🎓 课程作业说明

本项目为 University of Newcastle 的
INFT2051 – Mobile Application Development (.NET MAUI) 课程作业示例，
主要用于展示 .NET MAUI 跨平台应用架构设计、本地文件管理、多格式内容渲染、SQLite 数据存储、页面导航以及用户设置与状态持久化 等核心概念。

Coursework Notice
This project is developed as a coursework example for
INFT2051 – Mobile Application Development (.NET MAUI) at the University of Newcastle.
It demonstrates core concepts including cross-platform architecture, local file management, multi-format content rendering, SQLite persistence, page navigation, and user state management.

E-Book 📚

E-Book 是一个基于 .NET MAUI 的跨平台电子书阅读应用，支持
Android / iOS / macOS / Windows（受平台与目标框架支持限制）。

项目提供 多格式电子书阅读能力，包括文件导入、阅读进度保存、阅读设置、应用级设置以及 SQLite 本地数据存储，
适合作为课程项目、学习 .NET MAUI，或作为后续功能扩展的基础框架。

E-Book is a cross-platform e-book reader built with .NET MAUI, targeting
Android / iOS / macOS / Windows (subject to platform and target framework support).

It provides multi-format reading capabilities, including file import, reading progress tracking, reading preferences, app-level settings, and SQLite-based local storage.
The project is suitable for coursework, learning purposes, and future feature extension.

✨ 功能概览 | Features
📂 书架与文件管理 | Bookshelf & File Management

支持从设备导入本地文件

所有文件统一复制并存储至应用私有 Library 目录

自动防止重复文件导入

支持在书架页面直接删除文件

Import files directly from the device

All imported files are copied into a unified private Library directory

Duplicate file imports are automatically prevented

Files can be deleted directly from the bookshelf

支持格式 | Supported Formats

TXT

EPUB

PDF

HTML / HTM

DOCX

RTF

📖 阅读体验 | Reading Experience

滑动翻页（文本分页 / 章节级阅读）

字体大小可调并持久化保存

阅读主题（Light / Dark）自动恢复

简洁、无干扰的阅读界面设计

Swipe-based page or chapter navigation

Adjustable font size with persistence

Light / Dark reading themes with automatic restoration

Clean and distraction-free reading interface

⏱ 阅读进度 | Reading Progress

按书籍 / 文件独立记录阅读进度

再次打开时自动跳转至上次阅读位置

Per-book/file reading progress tracking

Automatically resumes from the last reading position

⚙️ 应用设置 | App Settings

启动密码（界面已完成，逻辑计划在后续版本实现）

退出锁定（可选）

阅读时保持屏幕常亮（可选）

Startup passcode (UI completed, logic planned)

Optional exit lock

Optional keep-screen-on option during reading

💾 数据与样式 | Data & Styling

使用 SQLite 本地数据库存储：

应用级设置

阅读设置

阅读进度

使用 ResourceDictionary 统一管理颜色与控件样式

SQLite local database is used to store:

App-level settings

Reading preferences

Reading progress

Centralized styling via ResourceDictionary

📖 多格式阅读实现 | Multi-Format Reading (V1.02)

不同文件类型采用针对性的阅读策略，而非统一按纯文本处理：

文件格式	处理方式
TXT	文本分页渲染
EPUB	HTML 渲染（章节级）
HTML / HTM	WebView 渲染
DOCX	转换为 HTML 后渲染
RTF	转换为 HTML 后渲染
PDF	使用系统外部阅读器打开（MVP 实现）

This design ensures that different document formats are rendered appropriately while maintaining application stability.

🧱 技术栈 | Tech Stack

.NET MAUI

C#

SQLite

WebView（HTML 渲染）

第三方库 | Third-party Libraries:

VersOne.Epub — EPUB parsing

Mammoth — DOCX to HTML conversion

RtfPipe — RTF to HTML conversion

📂 项目结构 | Project Structure
E-Book/
├─ Data/                # SQLite 数据库、数据模型、初始化逻辑
├─ Pages/               # 应用页面（Homepage / ReadingPage / SettingsPage）
├─ Resources/
│  ├─ Styles/           # 颜色与控件样式资源
│  └─ Fonts/            # 字体资源
├─ Platforms/           # 平台相关代码（Android / iOS / Windows / macOS）
├─ AppShell.xaml        # 路由与页面注册
└─ MauiProgram.cs       # 应用启动与依赖注册
📌 版本记录 | Version History
V1.02 — 多格式阅读功能整合 | Multi-Format Reading Integration

完成多格式文件导入与阅读流程闭环

Homepage 重构为统一书架结构

ReadingPage 增加基于文件类型的阅读分流逻辑

阅读进度与阅读设置稳定持久化

首次启动自动生成 Usage Guidelines 文档

🚧 已知限制 | Known Limitations

EPUB 章节标题暂未显示在顶部栏（计划优化）

PDF 暂不支持应用内渲染，仅使用系统外部阅读器

启动密码验证逻辑将在后续版本实现

🧭 未来计划 | Roadmap

 EPUB 章节标题与目录显示

 启动密码验证流程

 书签与阅读笔记

 搜索与文本高亮

📄 License

本项目目前未指定 License，仅用于学习与课程作业目的。
如需开源复用，可自行添加 MIT / Apache-2.0 License。
