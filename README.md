# 📚 E_Book

> A modern cross-platform e-book reader built with **.NET MAUI**  
> 基于 **.NET MAUI** 构建的现代跨平台电子书阅读应用

---

# 🏷 Version | 版本信息

![Version](https://img.shields.io/badge/version-v1.07-purple)
![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-blue)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub stars](https://img.shields.io/github/stars/Lab3r5/E_Book)

**Current Version:** `v1.07 – Multi-User Data Isolation & Performance Upgrade`  
**当前版本：** `v1.07 – 多用户数据隔离与性能优化`

---

# ✨ Project Overview | 项目简介

**E_Book** is a cross-platform mobile reading application developed with **.NET MAUI**.

The goal of the project is to build a **modern mobile reading experience** with:

- Clean UI design
- Structured application architecture
- Lightweight local storage
- Smooth reading interactions

E_Book 是一个基于 **.NET MAUI** 构建的跨平台移动阅读应用，目标是打造：

- 现代化阅读界面
- 清晰结构化架构
- 轻量级本地数据存储
- 流畅阅读体验

Developed for:

**INFT2051 – Mobile Application Development**  
University of Newcastle

---

# 🚀 Latest Release – v1.07
# 最新版本 – v1.07

Version **v1.07** introduces a **major system upgrade** focusing on **multi-user data isolation** and **performance improvements**.

v1.07 版本重点升级 **多用户数据隔离系统** 与 **整体性能优化**。

---

# ⭐ Key Features | 核心功能

## 📚 Smart Bookshelf System
## 智能书架系统

The bookshelf works like a modern commercial reading app dashboard.

书架系统模拟真实阅读应用。

Features:

- Reading progress display
- Continue reading indicators
- Recently opened sorting
- Automatic book cover generation

功能包括：

- 阅读进度显示
- Continue Reading 提示
- 最近阅读排序
- 自动生成书籍封面

---

## 👤 Multi-User Account System
## 多用户账号系统

E_Book now supports **independent user environments**.

每个账号拥有独立数据环境：

- Personal book library
- Independent reading progress
- Independent reading settings
- Independent search history
- Independent theme preferences

账号之间 **不会共享数据**。

---

## 👤 Guest Mode
## Guest 模式

Guest users run in a **separate storage profile**.

Guest 用户拥有独立存储空间：

- Guest library
- Guest reading history
- Guest preferences

不会影响注册用户数据。

---

## 📖 Reading Engine
## 阅读系统

The reading engine supports multiple formats.

阅读系统支持多种文档格式。

Supported formats:

- TXT
- EPUB
- DOCX
- RTF
- PDF (external reader)

Features:

- Adjustable font size
- Light / Dark reading themes
- Reading progress persistence
- Resume reading position

功能：

- 字体大小调整
- 深色 / 浅色主题
- 阅读进度自动保存
- 自动恢复阅读位置

---

# ⚡ Performance Optimization
# 性能优化

Version **v1.07** improves the responsiveness of the application.

v1.07 提升了整体应用流畅度。

Optimizations include:

- Smarter bookshelf refresh logic
- Reduced unnecessary page reloads
- Faster tab switching
- Improved navigation animations

优化包括：

- 智能书架刷新机制
- 减少页面重复加载
- 更快 Tab 切换
- 更流畅导航动画

---

# 🏗 Architecture | 技术架构

E_Book uses a modular architecture based on **.NET MAUI**.

架构特点：

- MAUI Shell navigation
- Service layer abstraction
- SQLite local database
- Preferences API storage
- User-based data isolation

---

# 📂 Project Structure | 项目结构

```

E_Book
│
├── Models
│   └── BookItem.cs
│
├── Pages
│   ├── BookshelfPage
│   ├── ReadingPage
│   ├── SearchPage
│   ├── SettingsHomePage
│   ├── LoginPage
│   └── SignUpPage
│
├── Services
│   ├── LibraryService.cs
│   ├── ReadingMetaStore.cs
│   ├── SearchHistoryStore.cs
│   └── ThemeScheduler.cs
│
├── Data
│   └── Database.cs
│
├── Resources
│   ├── Styles
│   ├── Images
│   └── TabIcons
│
└── AppShell.xaml

````

---

# 🔧 Tech Stack | 技术栈

- **C#**
- **.NET MAUI**
- **SQLite**
- **Shell Navigation**
- **Preferences API**

---

# 📦 Installation | 安装方式

Clone the repository:

```bash
git clone https://github.com/Lab3r5/E_Book.git
````

Open the solution in **Visual Studio 2022+**

```
Open → E_Book.sln
```

Run on:

* Android Emulator
* Android Device

---

# 📊 Version Evolution | 版本演进

| Version   | Description                  |
| --------- | ---------------------------- |
| v1.01     | Base reading system          |
| v1.02     | UI layout refinement         |
| v1.03     | Animated TabBar              |
| v1.04     | Settings redesign            |
| v1.05     | Smart bookshelf              |
| v1.06     | UI & navigation improvements |
| **v1.07** | Multi-user data isolation    |

---

# 🎯 Design Philosophy | 设计理念

E_Book emphasizes:

* Clean UI design
* Motion-driven interaction
* Structured architecture
* Minimalist purple theme
* Mobile-first experience

E_Book 强调：

* 清晰 UI
* 动效交互
* 结构化架构
* 紫色极简主题
* 移动优先体验

---

# 🛣 Future Roadmap | 后续规划

Planned features:

* Reading analytics dashboard
* Smart book recommendations
* MVVM architecture refactor
* Further performance optimization
* iOS support

未来计划：

* 阅读统计系统
* 智能书籍推荐
* MVVM 架构升级
* 性能优化
* iOS 支持

---

# 👨‍💻 Developer | 开发者

**Shen Jiawei**
University of Newcastle
Mobile Application Development

---

# 📜 License | 许可

This project is developed for **educational purposes**.
这样你的项目看起来 **像真正开源项目，而不是作业项目**，对 **GitHub portfolio 很加分**。
```
