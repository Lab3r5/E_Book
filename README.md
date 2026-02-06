---

````markdown
# 📘 E-Book

| 中文说明 | English Description |
|--------|---------------------|
| **课程作业说明**<br>本项目为 University of Newcastle 的 **INFT2051 – Mobile Application Development (.NET MAUI)** 课程作业示例，用于展示 .NET MAUI 跨平台应用架构、本地文件管理、多格式内容渲染、SQLite 数据存储、页面导航以及用户设置与状态持久化等核心概念。 | **Coursework Notice**<br>This project is developed as a coursework example for **INFT2051 – Mobile Application Development (.NET MAUI)** at the University of Newcastle. It demonstrates cross-platform architecture, local file management, multi-format content rendering, SQLite persistence, page navigation, and user state management. |

---

## ✨ 功能概览 | Features

| 中文功能 | English Features |
|--------|------------------|
| **书架与文件管理**<br>• 支持从设备导入本地文件<br>• 所有文件统一复制至应用私有 **Library** 目录<br>• 自动防止重复导入<br>• 支持在书架中直接删除文件 | **Bookshelf & File Management**<br>• Import files from the device<br>• Files are copied into a unified private **Library** directory<br>• Duplicate imports are automatically prevented<br>• Files can be deleted directly from the bookshelf |
| **支持格式**<br>TXT / EPUB / PDF / HTML / DOCX / RTF | **Supported Formats**<br>TXT / EPUB / PDF / HTML / DOCX / RTF |
| **阅读体验**<br>• 滑动翻页（文本 / 章节）<br>• 字体大小设置并持久化<br>• Light / Dark 阅读主题<br>• 简洁无干扰界面 | **Reading Experience**<br>• Swipe-based page/chapter navigation<br>• Adjustable font size with persistence<br>• Light / Dark reading themes<br>• Clean and distraction-free interface |
| **阅读进度**<br>• 按文件记录阅读进度<br>• 再次打开自动恢复 | **Reading Progress**<br>• Per-file reading progress tracking<br>• Automatically resumes from last position |
| **应用设置**<br>• 启动密码（UI 已完成）<br>• 退出锁定（可选）<br>• 阅读时保持屏幕常亮 | **App Settings**<br>• Startup passcode (UI completed)<br>• Optional exit lock<br>• Optional keep-screen-on option |

---

## 📖 多格式阅读实现 | Multi-Format Reading (V1.02)

| 文件格式 | 阅读方式 |
|--------|---------|
| TXT | 文本分页渲染 |
| EPUB | HTML 渲染（章节级） |
| HTML / HTM | WebView 渲染 |
| DOCX | 转换为 HTML 后渲染 |
| RTF | 转换为 HTML 后渲染 |
| PDF | 使用系统外部阅读器打开（MVP） |

---

## 🧱 技术栈 | Tech Stack

| 中文 | English |
|----|---------|
| .NET MAUI | .NET MAUI |
| C# | C# |
| SQLite | SQLite |
| WebView（HTML 渲染） | WebView (HTML Rendering) |
| VersOne.Epub（EPUB 解析） | VersOne.Epub (EPUB parsing) |
| Mammoth（DOCX → HTML） | Mammoth (DOCX to HTML) |
| RtfPipe（RTF → HTML） | RtfPipe (RTF to HTML) |

---

## 📂 项目结构 | Project Structure

```text
E-Book/
├─ Data/        # 数据库与数据模型
├─ Pages/       # 应用页面
├─ Resources/   # 样式与字体
├─ Platforms/   # 平台相关代码
├─ AppShell.xaml
└─ MauiProgram.cs
````

---

## 📌 版本记录 | Version History

| 版本        | 说明                                          |
| --------- | ------------------------------------------- |
| **V1.02** | 多格式文件导入与阅读闭环；ReadingPage 按格式分流；阅读进度与设置稳定持久化 |

---

## 🚧 已知限制 | Known Limitations

| 中文            | English                               |
| ------------- | ------------------------------------- |
| EPUB 章节标题暂未显示 | EPUB chapter titles not yet displayed |
| PDF 暂不支持应用内渲染 | PDF is opened externally              |
| 启动密码逻辑计划后续实现  | Password logic planned                |

---

## 🧭 未来计划 | Roadmap

| 计划功能         | Planned Features          |
| ------------ | ------------------------- |
| EPUB 章节标题与目录 | EPUB chapter titles & TOC |
| 启动密码验证流程     | Password verification     |
| 书签与阅读笔记      | Bookmarks & notes         |
| 搜索与文本高亮      | Search & highlighting     |
| 云同步          | Cloud sync                |

---

## 📄 License

本项目目前未指定 License，仅用于学习与课程作业目的。
No license is specified. This project is for learning and coursework purposes only.


---
