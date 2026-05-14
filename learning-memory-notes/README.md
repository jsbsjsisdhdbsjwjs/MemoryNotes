# 学习记忆笔记

一个用于快速记录学习笔记的本地 Web App。它支持先创建学习项目，再通过悬浮窗记录笔记，并根据标签、关键词和知识节点自动生成记忆关联网络。

## 功能

- 学习项目：先创建项目，再按项目管理笔记、知识树和关联网络。
- 悬浮记录窗：页面右下角可拖动，支持快速输入笔记。
- 桌面悬浮窗：可构建 Windows exe，关闭浏览器后仍可直接记录笔记。
- Obsidian 对接：桌面端保存笔记时，会按学习项目自动更新 Obsidian 项目笔记、运维笔记和 tags。
- 记忆关联网络：从 `#标签`、手动标签、知识树节点和笔记关键词自动生成概念关联。
- 知识树：支持根节点和父子节点，用于搭建课程/主题结构。
- 本地保存：网页端数据保存在浏览器 `localStorage`；桌面端默认保存到 E 盘。
- 导出 JSON：可导出当前全部项目数据。

## 使用

直接用浏览器打开：

```text
index.html
```

或者启动内置静态服务器：

```text
npm start
```

启动后访问：

```text
http://127.0.0.1:5173
```

## Windows 桌面悬浮窗

构建 exe：

```text
dotnet publish desktop-floating-note/DesktopFloatingNote.csproj -c Release -r win-x64 --self-contained false
```

运行生成文件：

```text
desktop-floating-note/bin/Release/net10.0-windows/win-x64/publish/MemoryNotesFloating.exe
```

桌面端数据默认保存位置：

```text
E:\MemoryNotes\data\notes.json
```

如果 E 盘不可用，会自动退回到：

```text
%APPDATA%\MemoryNotes\notes.json
```

说明：浏览器网页端受安全限制，不能直接把 `localStorage` 写到 E 盘；需要长期保存时请使用导出 JSON，或优先使用桌面悬浮窗 exe。

桌面端同步 Obsidian：

```text
C:\Users\JC\Documents\Obsidian Vault\Projects\<项目名>.md
C:\Users\JC\Documents\Obsidian Vault\Ops\<项目名> Ops.md
```

保存笔记时会自动创建缺失的项目笔记和运维笔记，并把用户输入的标签和关键词写入 frontmatter tags 与运维条目。

## 后续优化方向

- 增加全文搜索高亮。
- 增加笔记反链和概念详情侧栏。
- 增加 Markdown 编辑与预览。
- 增加 IndexedDB 存储，支持更大数据量。
- 增加 GitHub Pages 部署。
