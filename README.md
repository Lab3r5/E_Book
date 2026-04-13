# 📚 E_Book

> A modern cross-platform e-book reader built with **.NET MAUI**  
> 基于 **.NET MAUI** 构建的现代跨平台电子书阅读应用

---

# 🏷 Version | 版本信息

![Version](https://img.shields.io/badge/version-v1.10-purple)
![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-blue)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub stars](https://img.shields.io/github/stars/Lab3r5/E_Book)

**Current Version:** `v1.10 - Stability Improvements, Reader Optimization & UX Refinement`  
**当前版本：** `v1.10 - 稳定性增强、阅读器优化与体验完善`

---

# ✨ Project Overview | 项目简介

**E_Book** is a cross-platform mobile reading application developed with **.NET MAUI**.  
It is designed to provide a smooth, product-like reading experience with local persistence, user isolation, and support for multiple book formats.

**E_Book** 是一个基于 **.NET MAUI** 构建的跨平台移动阅读应用，目标是提供接近真实产品的阅读体验，并兼顾：

- Modern reading UI
- Structured application architecture
- Lightweight local data persistence
- Multi-user isolation
- Smooth reading interaction
- Practical bookshelf and search workflows

项目特性包括：

- 现代化阅读界面
- 清晰可维护的应用架构
- 轻量级本地数据存储
- 多用户数据隔离
- 流畅阅读交互
- 实用的书架与搜索体验

Developed for:  
**INFT2051 - Mobile Application Development**  
**University of Newcastle**

---

# 🚀 Latest Release - v1.10

# 最新版本 - v1.10

Version **v1.10** focuses on stabilizing the overall application, improving reader performance, refining navigation behaviour, and making the reading flow more consistent across supported formats.

**v1.10** 重点在于提升应用整体稳定性、优化阅读器性能、改善导航与页面衔接，并让多格式阅读体验更加一致。

### Key improvements | 重点改进

- Improved reader loading behaviour for large documents
- Added parsing and pagination cache for supported document formats
- Improved TOC accuracy for EPUB and document-based reading
- Better bookshelf refresh behaviour after reading
- Improved search preloading and result responsiveness
- Reduced redundant UI refresh and repeated calculations
- Refined page-level UI consistency across major modules

具体优化包括：

- 优化大文档打开流程
- 为支持的文档格式加入解析缓存与分页缓存
- 提升 EPUB 与文档类书籍的 TOC 准确性
- 改善阅读返回书架后的进度刷新
- 优化搜索页预加载与结果响应速度
- 减少重复 UI 刷新与重复计算
- 提升主要页面的视觉一致性

---

# ⭐ Key Features | 核心功能

## 📚 Smart Bookshelf System

## 智能书架系统

- Reading progress display
- Continue reading indicator
- Reading percentage and latest progress info
- Recent reading metadata updates
- Auto-generated color cover for local files
- User-based library isolation

功能包括：

- 阅读进度显示
- 继续阅读标识
- 阅读百分比与最近阅读信息
- 最近阅读元数据更新
- 本地图书自动生成封面色块
- 按用户隔离书库数据

---

## 👤 Multi-User Account System

## 多用户账号系统

Each user has independent:

- Library
- Reading progress
- Reading settings
- Search history
- Session data

每个账号拥有独立的：

- 书库
- 阅读进度
- 阅读设置
- 搜索历史
- 会话数据

---

## 👤 Guest Mode

## Guest 模式

- Independent storage profile
- No impact on registered users
- Suitable for quick local usage

特点：

- 独立存储空间
- 不影响注册用户数据
- 适合快速本地体验

---

## 📖 Reading Engine

## 阅读系统

### Supported formats | 支持格式

- TXT
- EPUB
- HTML
- DOCX
- RTF
- PDF
- Image-based reading files

### Reading features | 阅读功能

- Font size control
- Line spacing adjustment
- Theme switching
- Progress saving and resume reading
- TOC navigation
- Page-based reading interaction
- Reading duration tracking
- Per-book reading settings

支持的阅读能力包括：

- 字体大小调整
- 行距调整
- 阅读主题切换
- 进度保存与断点续读
- TOC 目录跳转
- 分页式阅读交互
- 阅读时长统计
- 按书保存阅读设置

---

## 🔎 Search Experience

## 搜索体验

- Real-time local search
- Search history chip list
- Result card interaction
- Fast re-entry with cached library loading

功能包括：

- 本地图书实时搜索
- 搜索历史记录标签
- 搜索结果卡片交互
- 基于缓存的快速重新进入搜索页

---

## 🔔 Notification Support

## 通知支持

- Reading reminder preferences
- Continue reading reminders
- Import-related notification settings

支持内容：

- 阅读提醒偏好设置
- 继续阅读提醒
- 导入相关通知设置

---

# 📖 Reading Experience Improvements

# 阅读体验优化

Version **v1.10** significantly improves the reader pipeline, especially for document-based formats such as **DOCX / RTF / HTML**.

**v1.10** 对阅读器链路进行了较大优化，尤其针对 **DOCX / RTF / HTML** 这类文档格式进行了重点提升。

### Improvements | 优化内容

- Cached document parsing results
- Added disk-backed pagination cache
- Faster reopen for previously opened large books
- More stable restore-to-progress behaviour
- Better TOC positioning logic
- More reliable first-page display flow

具体效果：

- 文档解析结果可缓存
- 新增分页结果磁盘缓存
- 再次打开大书时速度更快
- 阅读进度恢复更加稳定
- TOC 定位逻辑更准确
- 首屏展示流程更可靠

---

# ⚡ Performance Optimization | 性能优化

The project includes several practical performance improvements focused on real user scenarios.

项目目前已经包含多项围绕真实使用场景的性能优化：

- Library directory caching
- Search page async preload
- Bookshelf summary aggregation caching
- Document parsing cache
- Document pagination cache
- Reduced redundant progress and metadata refresh

已经落地的优化包括：

- 书库目录缓存
- 搜索页异步预加载
- 书架顶部摘要聚合缓存
- 文档解析缓存
- 文档分页结果缓存
- 减少重复进度保存与元数据刷新

---

# 🎨 UI & UX Improvements | 界面与交互优化

Version **v1.10** also improves UI consistency across multiple key modules.

v1.10 同时对多个关键页面做了视觉与交互统一：

- Cleaner page hierarchy
- Improved consistency across authentication, bookshelf, search, and settings modules
- More stable animation behaviour
- Better visual feedback for reading and search flows

改进方向包括：

- 更清晰的页面层次
- 认证、书架、搜索、设置页视觉更统一
- 动画表现更稳定
- 阅读与搜索流程反馈更自然

---

# 🏗 Architecture | 技术架构

The project follows a relatively clear page + service + local data structure.

项目整体采用较清晰的“页面 + 服务 + 本地数据”结构：

- MAUI Shell navigation for route-based navigation
- SQLite/local database helpers for app data persistence
- Preferences API for lightweight settings storage
- Service layer for parsing, caching, library loading, notifications, and animations
- User-based isolation for library, settings, and history

核心架构包括：

- 基于 MAUI Shell 的路由导航
- 基于 SQLite / 本地数据库工具 的数据持久化
- 使用 Preferences API 存储轻量设置
- 使用 Service 层 管理解析、缓存、书库、通知、动画等逻辑
- 基于用户身份隔离书库、设置与历史数据

---

# 📂 Project Structure | 项目结构

```text
E_Book
│
├── Common
├── Controls
├── Data
│   └── Database.cs
├── Models
├── Pages
│   ├── LoginPage
│   ├── SignUpPage
│   ├── ForgotPasswordPage
│   ├── ResetPasswordPage
│   ├── BookshelfPage
│   ├── ReadingPage
│   ├── PdfReaderPage
│   ├── SearchPage
│   ├── SettingsHomePage
│   ├── EditProfilePage
│   ├── AppearancePage
│   ├── NotificationsPage
│   ├── HelpCenterPage
│   └── PrivacyPolicyPage
├── Platforms
├── Properties
├── Resources
├── Services
│   ├── CachedDocumentContentService.cs
│   ├── DocumentContentService.cs
│   ├── DocumentPaginationCacheService.cs
│   ├── LibraryService.cs
│   ├── NotificationService.cs
│   ├── ReadingMetaStore.cs
│   ├── SearchHistoryStore.cs
│   └── UIAnimationService.cs
├── App.xaml
├── AppShell.xaml
├── MauiProgram.cs
└── E_Book.csproj
````

---

# 🔧 Tech Stack | 技术栈

* C#
* .NET MAUI
* SQLite / local persistence
* Shell Navigation
* Preferences API
* XAML UI
* Service-oriented application structure

---

# 📦 Installation | 安装方式

```bash
git clone https://github.com/Lab3r5/E_Book.git
```

Open the project in:

Visual Studio 2022 or later
.NET MAUI workload installed

Recommended run targets:

* Android Emulator
* Android Device
* Windows (for local build verification)

推荐环境：

* Visual Studio 2022+
* 已安装 .NET MAUI workload
* Android 模拟器或真机
* Windows 本地构建验证环境

---

# ▶️ Build Notes | 构建说明

For Windows local verification:

```powershell
dotnet build E_Book.csproj -f net9.0-windows10.0.19041.0
```

Notes:

* Windows build is useful for fast verification during development.
* Android build requires the corresponding Android SDK workload and platform packages.

说明：

* Windows 构建适合开发阶段快速验证。
* Android 构建需要安装对应 Android SDK 与 MAUI workload。

---

# 📊 Version Evolution | 版本演进

| Version | Description                                                |
| ------- | ---------------------------------------------------------- |
| v1.01   | Base reading system                                        |
| v1.02   | UI layout refinement                                       |
| v1.03   | Animated TabBar                                            |
| v1.04   | Settings redesign                                          |
| v1.05   | Smart bookshelf                                            |
| v1.06   | UI improvements                                            |
| v1.07   | Multi-user isolation                                       |
| v1.08   | Settings refinement                                        |
| v1.09   | Account security system                                    |
| v1.10   | Stability, reader optimization, caching, and UX refinement |

---

# 🎯 Design Philosophy | 设计理念

* Clean UI
* Smooth interaction
* Structured architecture
* Practical local-first experience
* Real-world product alignment

设计目标：

* 简洁界面
* 流畅交互
* 清晰结构
* 以本地优先体验为核心
* 尽量接近真实产品形态

---

# 🛣 Future Roadmap | 后续规划

* Further MVVM refactoring
* Broader platform polishing
* More reader analytics
* Smarter recommendations
* Additional performance tuning

后续方向：

* 进一步推进 MVVM 化
* 完善更多平台体验
* 增加阅读分析能力
* 更智能的推荐能力
* 持续性能优化

---

# 👨‍💻 Developer | 开发者

Shen Jiawei
University of Newcastle

---

# 📜 License | 许可

This project is developed for educational purposes.

本项目主要用于学习与课程开发用途。
