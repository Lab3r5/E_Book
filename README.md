---

```markdown
# 📚 E_Book

> A modern cross-platform e-book reader built with .NET MAUI  
> 基于 .NET MAUI 构建的现代跨平台电子书阅读应用

---

# 🏷 Version | 版本信息

![Version](https://img.shields.io/badge/version-v1.05-purple)
![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-blue)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub stars](https://img.shields.io/github/stars/Lab3r5/E_Book)

Current Version: **v1.05 – Smart Bookshelf System**  
当前版本：**v1.05 – 智能书架系统升级**

---

# 📱 App Preview | 应用预览

### Bookshelf Interface

The redesigned bookshelf now displays reading progress, automatically generated book covers, and recently opened books.

重新设计的书架界面现在可以显示阅读进度、自动生成的书籍封面以及最近阅读书籍。

*(Preview screenshots can be added here)*

---

# ✨ Project Overview | 项目简介

**E_Book** is a cross-platform mobile e-book reader developed using **.NET MAUI**.

The application focuses on:

- Clean UI design
- Structured mobile architecture
- Modern reading experience
- Lightweight local storage

E_Book 是一个基于 **.NET MAUI** 构建的跨平台电子书阅读应用，重点关注：

- 清晰的 UI 设计
- 结构化移动应用架构
- 现代阅读体验
- 轻量级本地数据存储

Developed for:

**INFT2051 – Mobile Application Development**  
University of Newcastle

---

# 🚀 Latest Release – v1.05  
# 最新版本 – v1.05

## 🧠 Smart Bookshelf System Upgrade  
## 智能书架系统升级

Version **v1.05** introduces a major upgrade to the bookshelf system, transforming the static book list into a smart reading dashboard.

v1.05 版本对书架系统进行了全面升级，使书架从简单的文件列表变为智能阅读管理界面。

Key improvements include:

- Automatic book cover generation
- Reading progress tracking
- Continue reading indicators
- Recently opened sorting
- Metadata persistence system

---

# 📚 Core Features | 核心功能

---

# 📖 Reading System | 阅读系统

The reading system supports multiple document formats and provides a minimal distraction reading environment.

阅读系统支持多种文档格式，并提供简洁的阅读体验。

Features:

- TXT file reading
- EPUB support
- PDF external reader support
- DOCX / RTF support
- Adjustable font size
- Light / Dark reading theme
- Reading progress persistence
- Resume reading from last position

功能包括：

- TXT 文本阅读
- EPUB 电子书支持
- PDF 外部阅读器支持
- DOCX / RTF 文档支持
- 字体大小调整
- 深色 / 浅色阅读模式
- 阅读进度自动保存
- 自动恢复阅读位置

---

# 📚 Smart Bookshelf System (v1.05)  
# 智能书架系统（v1.05）

The bookshelf system has been redesigned to behave more like a real commercial reading application.

书架系统经过重构，提供更接近商业阅读应用的体验。

---

## 📖 Automatic Book Cover Generation  
## 自动生成书籍封面

Each imported book automatically receives a generated cover.

每本导入的书籍都会自动生成封面。

The system extracts characters from the book title to create a visual identifier.

系统会根据书名提取字符生成封面文字。

Examples:

```

Cyberpunk2077 → CY
Usage Guidelines → UG
中国小说 → 中国

```

Each cover also receives a generated color palette.

每个封面都会自动生成颜色主题。

---

## 📊 Reading Progress Tracking  
## 阅读进度显示

Bookshelf now shows:

- Reading progress bar
- Reading percentage
- Continue reading indicator

书架现在可以显示：

- 阅读进度条
- 阅读百分比
- Continue Reading 提示

This allows users to quickly identify partially read books.

方便用户快速识别未读完的书籍。

---

## 🕒 Recently Opened Sorting  
## 最近阅读排序

Books are automatically sorted by last opened time.

书籍会根据最近阅读时间自动排序。

Recently opened books appear at the top of the bookshelf.

最近阅读的书籍会自动显示在最前面。

---

## 🧠 Reading Metadata System

A new service **ReadingMetaStore** is introduced.

新增 **ReadingMetaStore** 服务。

This service manages:

- Reading progress
- Last opened timestamp
- Reading metadata persistence

用于管理：

- 阅读进度
- 最近阅读时间
- 阅读元数据存储

---

# ⚙ Bookshelf Interaction System  
# 书架交互系统

The bookshelf includes modern mobile interaction patterns.

书架包含现代移动应用交互方式。

Supported interactions:

- Swipe to delete books
- Multi-select delete mode
- Select All / Cancel actions
- Animated delete confirmation dialog
- Toast notification system

支持：

- 左滑删除书籍
- 多选删除模式
- 全选 / 取消操作
- 删除确认弹窗动画
- Toast 提示

---

# 🎨 UI Design Improvements (v1.05)  
# UI 界面优化（v1.05）

Major visual improvements include:

- Redesigned bookshelf layout
- Balanced book cover size
- Compact reading progress bar
- Better title readability
- Improved small-screen compatibility
- Cleaner multi-select header layout

界面优化包括：

- 重新设计书架布局
- 调整书籍封面比例
- 优化阅读进度条
- 提升书名可读性
- 小屏设备适配优化
- 多选模式头部布局优化

---

# 🏗 Architecture | 技术架构

E_Book uses a modular mobile architecture based on **.NET MAUI**.

E_Book 采用基于 **.NET MAUI** 的模块化架构。

Architecture highlights:

- MAUI Shell navigation
- Modular page system
- Service layer abstraction
- Local metadata persistence
- Cross-platform UI design

架构特点：

- MAUI Shell 路由导航
- 模块化页面结构
- Service 层解耦
- 本地元数据存储
- 跨平台 UI 设计

---

# 📂 Project Structure | 项目结构

```

E_Book
│
├── Models
│   └── BookItem.cs
│
├── Pages
│   ├── BookshelfPage.xaml
│   ├── BookshelfPage.xaml.cs
│   ├── ReadingPage.xaml
│   ├── ReadingPage.xaml.cs
│   ├── SearchPage
│   └── SettingsPage
│
├── Services
│   ├── LibraryService.cs
│   └── ReadingMetaStore.cs
│
├── Resources
│   ├── Styles
│   └── Images
│
└── AppShell.xaml

```

---

# 🔧 Tech Stack | 技术栈

- **C#**
- **.NET MAUI**
- **SQLite**
- **Shell Navigation**
- **Preferences API**
- **Android Material Components**

---

# 📊 Version Evolution | 版本演进

| Version | Description | 说明 |
|-------|-------------|------|
| v1.01 | Base reading system | 阅读基础功能 |
| v1.02 | UI layout refinement | UI 布局优化 |
| v1.03 | Custom animated TabBar | 自定义动画 TabBar |
| v1.04 | Flagship Settings redesign | Settings 页面升级 |
| v1.05 | Smart Bookshelf system | 智能书架系统 |

---

# 🎯 Design Philosophy | 设计理念

E_Book emphasizes:

- Clean UI design  
- Motion-driven interaction  
- Structured architecture  
- Minimalist purple theme  
- Mobile-first design

E_Book 强调：

- 清晰 UI 设计
- 动效交互体验
- 结构化应用架构
- 紫色极简主题
- 移动优先设计

---

# 🛣 Future Roadmap | 后续规划

Planned future features:

- Reading analytics dashboard
- Smart book recommendations
- MVVM architecture refactor
- Performance profiling
- iOS UI refinement

未来计划功能：

- 阅读统计系统
- 智能书籍推荐
- MVVM 架构重构
- 性能优化
- iOS 平台 UI 优化

---

# 👨‍💻 Developer | 开发者

**Shen Jiawei**  
University of Newcastle  
Mobile Application Development

---

# 📜 License | 许可说明

This project is developed for educational purposes.

本项目用于教学与学习用途。
```

---
