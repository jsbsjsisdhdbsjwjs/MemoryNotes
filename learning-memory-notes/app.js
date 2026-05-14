const STORAGE_KEY = "learning-memory-notes:v1";

const state = loadState();

const els = {
  projectForm: document.querySelector("#projectForm"),
  projectName: document.querySelector("#projectName"),
  projectList: document.querySelector("#projectList"),
  currentProjectTitle: document.querySelector("#currentProjectTitle"),
  noteProject: document.querySelector("#noteProject"),
  noteForm: document.querySelector("#noteForm"),
  noteContent: document.querySelector("#noteContent"),
  noteTags: document.querySelector("#noteTags"),
  noteList: document.querySelector("#noteList"),
  treeForm: document.querySelector("#treeForm"),
  treeTitle: document.querySelector("#treeTitle"),
  treeParent: document.querySelector("#treeParent"),
  knowledgeTree: document.querySelector("#knowledgeTree"),
  searchInput: document.querySelector("#searchInput"),
  exportBtn: document.querySelector("#exportBtn"),
  canvas: document.querySelector("#networkCanvas"),
  noteCount: document.querySelector("#noteCount"),
  treeCount: document.querySelector("#treeCount"),
  linkCount: document.querySelector("#linkCount"),
  floatWindow: document.querySelector("#floatWindow"),
  floatHeader: document.querySelector("#floatHeader"),
  toggleFloat: document.querySelector("#toggleFloat"),
  hideFloat: document.querySelector("#hideFloat"),
};

function loadState() {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (raw) return JSON.parse(raw);

  const defaultProject = {
    id: crypto.randomUUID(),
    name: "默认学习项目",
    createdAt: new Date().toISOString(),
  };

  return {
    activeProjectId: defaultProject.id,
    projects: [defaultProject],
    notes: [],
    treeNodes: [],
  };
}

function saveState() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function activeProject() {
  return state.projects.find((project) => project.id === state.activeProjectId);
}

function projectNotes() {
  const query = els.searchInput.value.trim().toLowerCase();
  return state.notes
    .filter((note) => note.projectId === state.activeProjectId)
    .filter((note) => {
      if (!query) return true;
      const haystack = [note.content, note.tags.join(" "), note.links.join(" ")].join(" ").toLowerCase();
      return haystack.includes(query);
    })
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
}

function projectTreeNodes() {
  return state.treeNodes.filter((node) => node.projectId === state.activeProjectId);
}

function extractTags(content, explicitTags) {
  const hashTags = [...content.matchAll(/#([\p{L}\p{N}_-]+)/gu)].map((match) => match[1]);
  const typedTags = explicitTags
    .split(/[,，]/)
    .map((tag) => tag.trim())
    .filter(Boolean);
  return [...new Set([...hashTags, ...typedTags])];
}

function extractLinks(content, tags) {
  const treeTitles = projectTreeNodes().map((node) => node.title);
  const matchedTree = treeTitles.filter((title) => title && content.includes(title));
  const keywords = content
    .replace(/#[\p{L}\p{N}_-]+/gu, " ")
    .split(/[^\p{L}\p{N}]+/u)
    .map((word) => word.trim())
    .filter((word) => word.length >= 2 && word.length <= 12)
    .slice(0, 12);
  return [...new Set([...tags, ...matchedTree, ...keywords])];
}

function renderProjects() {
  els.projectList.innerHTML = "";
  state.projects.forEach((project) => {
    const item = document.createElement("div");
    item.className = `project-item ${project.id === state.activeProjectId ? "active" : ""}`;
    item.innerHTML = `<strong>${escapeHtml(project.name)}</strong><span>${countProjectNotes(project.id)} 条</span>`;
    item.addEventListener("click", () => {
      state.activeProjectId = project.id;
      saveState();
      render();
    });
    els.projectList.appendChild(item);
  });

  els.noteProject.innerHTML = state.projects
    .map((project) => `<option value="${project.id}">${escapeHtml(project.name)}</option>`)
    .join("");
  els.noteProject.value = state.activeProjectId;
}

function countProjectNotes(projectId) {
  return state.notes.filter((note) => note.projectId === projectId).length;
}

function renderTree() {
  const nodes = projectTreeNodes();
  els.treeParent.innerHTML = `<option value="">作为根节点</option>${nodes
    .map((node) => `<option value="${node.id}">${escapeHtml(node.title)}</option>`)
    .join("")}`;

  const ordered = flattenTree(nodes);
  els.knowledgeTree.innerHTML = ordered.length
    ? ordered
        .map(
          ({ node, depth }) =>
            `<div class="tree-node" data-depth="${Math.min(depth, 3)}"><strong>${escapeHtml(node.title)}</strong><div class="tags">${linkedNoteBadges(node.title)}</div></div>`,
        )
        .join("")
    : `<div class="empty">先添加一个知识节点</div>`;
}

function linkedNoteBadges(title) {
  const count = state.notes.filter((note) => note.projectId === state.activeProjectId && note.links.includes(title)).length;
  return count ? `<span class="tag">${count} 条关联笔记</span>` : `<span class="tag">待关联</span>`;
}

function flattenTree(nodes) {
  const byParent = new Map();
  nodes.forEach((node) => {
    const parent = node.parentId || "root";
    byParent.set(parent, [...(byParent.get(parent) || []), node]);
  });

  const output = [];
  const walk = (parentId, depth) => {
    (byParent.get(parentId) || [])
      .sort((a, b) => a.createdAt.localeCompare(b.createdAt))
      .forEach((node) => {
        output.push({ node, depth });
        walk(node.id, depth + 1);
      });
  };
  walk("root", 0);
  return output;
}

function renderNotes() {
  const notes = projectNotes();
  els.noteList.innerHTML = notes.length
    ? notes
        .map(
          (note) => `<article class="note-item">
            <h3>${formatDate(note.createdAt)}</h3>
            <p>${escapeHtml(note.content)}</p>
            <div class="tags">${note.tags.map((tag) => `<span class="tag">#${escapeHtml(tag)}</span>`).join("")}</div>
          </article>`,
        )
        .join("")
    : `<div class="empty">悬浮窗里输入第一条笔记</div>`;
}

function renderMetrics() {
  const notes = state.notes.filter((note) => note.projectId === state.activeProjectId);
  const trees = projectTreeNodes();
  const links = new Set(notes.flatMap((note) => note.links.map((link) => `${note.id}:${link}`)));
  els.currentProjectTitle.textContent = activeProject()?.name || "未选择";
  els.noteCount.textContent = notes.length;
  els.treeCount.textContent = trees.length;
  els.linkCount.textContent = links.size;
}

function renderNetwork() {
  const canvas = els.canvas;
  const ctx = canvas.getContext("2d");
  const rect = canvas.getBoundingClientRect();
  canvas.width = Math.floor(rect.width * devicePixelRatio);
  canvas.height = Math.floor(rect.height * devicePixelRatio);
  ctx.scale(devicePixelRatio, devicePixelRatio);
  const width = rect.width;
  const height = rect.height;
  ctx.clearRect(0, 0, width, height);

  const notes = projectNotes();
  const concepts = [...new Set(notes.flatMap((note) => note.links))].slice(0, 28);
  const nodes = [
    ...notes.slice(0, 18).map((note, index) => ({ id: note.id, label: `笔记 ${notes.length - index}`, type: "note", note })),
    ...concepts.map((label) => ({ id: `concept:${label}`, label, type: "concept" })),
  ];

  if (!nodes.length) {
    ctx.fillStyle = "#647086";
    ctx.font = "15px Segoe UI";
    ctx.fillText("保存笔记后自动生成关联网络", 24, 40);
    return;
  }

  const center = { x: width / 2, y: height / 2 };
  const radius = Math.min(width, height) * 0.36;
  nodes.forEach((node, index) => {
    const angle = (index / nodes.length) * Math.PI * 2 - Math.PI / 2;
    node.x = center.x + Math.cos(angle) * radius * (node.type === "note" ? 0.78 : 1.05);
    node.y = center.y + Math.sin(angle) * radius * (node.type === "note" ? 0.78 : 1.05);
  });

  ctx.lineWidth = 1.5;
  notes.slice(0, 18).forEach((note) => {
    const source = nodes.find((node) => node.id === note.id);
    note.links.forEach((link) => {
      const target = nodes.find((node) => node.id === `concept:${link}`);
      if (!source || !target) return;
      ctx.strokeStyle = "rgba(37, 99, 235, 0.2)";
      ctx.beginPath();
      ctx.moveTo(source.x, source.y);
      ctx.lineTo(target.x, target.y);
      ctx.stroke();
    });
  });

  nodes.forEach((node) => {
    ctx.beginPath();
    ctx.fillStyle = node.type === "note" ? "#2563eb" : "#0f8f72";
    ctx.arc(node.x, node.y, node.type === "note" ? 12 : 16, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#172033";
    ctx.font = "13px Segoe UI";
    ctx.fillText(trimLabel(node.label), node.x + 18, node.y + 4);
  });
}

function render() {
  renderProjects();
  renderTree();
  renderNotes();
  renderMetrics();
  renderNetwork();
}

function escapeHtml(value) {
  return value.replace(/[&<>"']/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[char]);
}

function formatDate(value) {
  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

function trimLabel(value) {
  return value.length > 12 ? `${value.slice(0, 12)}...` : value;
}

els.projectForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const name = els.projectName.value.trim();
  if (!name) return;
  const project = { id: crypto.randomUUID(), name, createdAt: new Date().toISOString() };
  state.projects.push(project);
  state.activeProjectId = project.id;
  els.projectName.value = "";
  saveState();
  render();
});

els.noteProject.addEventListener("change", () => {
  state.activeProjectId = els.noteProject.value;
  saveState();
  render();
});

els.noteForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const content = els.noteContent.value.trim();
  if (!content) return;
  const tags = extractTags(content, els.noteTags.value);
  const note = {
    id: crypto.randomUUID(),
    projectId: state.activeProjectId,
    content,
    tags,
    links: extractLinks(content, tags),
    createdAt: new Date().toISOString(),
  };
  state.notes.push(note);
  els.noteContent.value = "";
  els.noteTags.value = "";
  saveState();
  render();
});

els.treeForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const title = els.treeTitle.value.trim();
  if (!title) return;
  state.treeNodes.push({
    id: crypto.randomUUID(),
    projectId: state.activeProjectId,
    title,
    parentId: els.treeParent.value || null,
    createdAt: new Date().toISOString(),
  });
  els.treeTitle.value = "";
  saveState();
  render();
});

els.searchInput.addEventListener("input", render);

els.exportBtn.addEventListener("click", () => {
  const data = JSON.stringify(state, null, 2);
  const blob = new Blob([data], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "learning-memory-notes-export.json";
  link.click();
  URL.revokeObjectURL(url);
});

els.toggleFloat.addEventListener("click", () => els.floatWindow.classList.remove("hidden"));
els.hideFloat.addEventListener("click", () => els.floatWindow.classList.add("hidden"));

let drag = null;
els.floatHeader.addEventListener("pointerdown", (event) => {
  const box = els.floatWindow.getBoundingClientRect();
  drag = { dx: event.clientX - box.left, dy: event.clientY - box.top };
  els.floatHeader.setPointerCapture(event.pointerId);
});

els.floatHeader.addEventListener("pointermove", (event) => {
  if (!drag) return;
  els.floatWindow.style.left = `${event.clientX - drag.dx}px`;
  els.floatWindow.style.top = `${event.clientY - drag.dy}px`;
  els.floatWindow.style.right = "auto";
  els.floatWindow.style.bottom = "auto";
});

els.floatHeader.addEventListener("pointerup", () => {
  drag = null;
});

window.addEventListener("resize", renderNetwork);

render();
