# 📚 E_Book

> A modern cross-platform e-book reader built with **.NET MAUI**  
> 基于 **.NET MAUI** 构建的现代跨平台电子书阅读应用

---

# 🏷 Version | 版本信息

![Version](https://img.shields.io/badge/version-v1.06-purple)
![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-blue)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub stars](https://img.shields.io/github/stars/Lab3r5/E_Book)

**Current Version:** `v1.06 – UI & Navigation Enhancement`  
**当前版本：** `v1.06 – UI 与导航体验升级`

---

# 📱 App Preview | 应用预览

### Bookshelf Interface

The redesigned bookshelf now includes a smarter layout and improved reading indicators.

重新设计的书架界面现在包含更加智能的布局以及阅读状态提示。

Main improvements:

- Reading progress indicator
- Continue reading status
- Automatically generated book covers
- Modern empty-state onboarding interface

*(Preview screenshots can be added here)*

---

# ✨ Project Overview | 项目简介

**E_Book** is a cross-platform mobile reading application developed using **.NET MAUI**.

The project aims to build a **modern mobile reading experience** with:

- Clean UI design
- Structured architecture
- Lightweight data storage
- Smooth reading interactions

E_Book 是一个基于 **.NET MAUI** 构建的跨平台电子书阅读应用，目标是打造：

- 现代化阅读界面
- 清晰结构化架构
- 轻量级数据存储
- 流畅阅读体验

Developed for:

**INFT2051 – Mobile Application Development**  
University of Newcastle

---

# 🚀 Latest Release – v1.06
# 最新版本 – v1.06

Version **v1.06** focuses on improving the **user interface, navigation experience, and visual consistency**.

v1.06 版本重点优化 **UI 设计、导航体验以及视觉一致性**。

---

# 🎨 UI & UX Improvements (v1.06)
# UI 与用户体验优化

## Redesigned "Add Book" Button
## Add Book 按钮重新设计

The bottom action button has been redesigned into a **modern card-style CTA**.

底部按钮升级为 **卡片式操作按钮**。

New improvements include:

- Gradient background design
- Action icon (+)
- Title + subtitle layout
- Improved visual hierarchy
- Clearer call-to-action guidance

新的设计包括：

- 渐变背景
- + 操作图标
- 主标题 + 副标题结构
- 更清晰视觉层级
- 更直观操作引导

---

## Empty Library Experience
## 空书架引导界面

When no books exist in the library, users now see a friendly onboarding interface.

当书架为空时，系统会显示引导界面。

Features:

- Empty state illustration
- Instruction text
- Import book button

功能包括：

- 空书架图标
- 引导说明
- 添加书籍按钮

This improves the **first-time user experience**.

---

# 📚 Smart Bookshelf System
# 智能书架系统

The bookshelf behaves like a **modern commercial reading app dashboard**.

书架系统模拟真实阅读应用。

Features:

- Reading progress display
- Continue reading indicators
- Recently opened sorting
- Smart book cover generation

主要功能：

- 阅读进度显示
- Continue reading 提示
- 最近阅读排序
- 自动书籍封面

---

## 📖 Automatic Book Cover Generation
## 自动书籍封面生成

Each imported book automatically generates a cover.

每本导入书籍都会自动生成封面。

Example:

```

Cyberpunk2077 → CY
Usage Guidelines → UG
中国小说 → 中国

```

Each cover also receives a unique color palette.

每本书都会生成独立颜色主题。

---

## 📊 Reading Progress Tracking
## 阅读进度显示

The bookshelf now displays:

- Reading progress bar
- Reading percentage
- Continue reading indicator

书架可以显示：

- 阅读进度条
- 阅读百分比
- Continue reading 提示

Users can easily resume unfinished books.

---

# 📖 Reading System | 阅读系统

The reading engine supports multiple document formats.

阅读系统支持多种文档格式。

Supported features:

- TXT reading
- EPUB support
- PDF external reader support
- DOCX / RTF support
- Adjustable font size
- Light / Dark reading themes
- Reading progress persistence
- Resume from last reading position

支持功能：

- TXT 阅读
- EPUB 支持
- PDF 外部阅读
- DOCX / RTF 文档
- 字体大小调整
- 深色 / 浅色主题
- 阅读进度自动保存
- 自动恢复阅读位置

---

# ⚙ Interaction System
# 交互系统

The bookshelf includes modern mobile interaction patterns.

书架支持现代移动应用交互。

Supported interactions:

- Swipe to delete
- Multi-select delete
- Select all / cancel
- Animated delete confirmation dialog
- Toast notification system

支持：

- 左滑删除
- 多选删除
- 全选 / 取消
- 删除确认弹窗
- Toast 提示

---

# 📱 Navigation System (v1.06)
# 导航系统升级

The bottom navigation bar has been redesigned.

底部导航栏进行了升级。

Improvements include:

- Custom tab icons
- Selected / unselected icon states
- Improved icon scaling
- Smoother highlight animation

优化包括：

- 自定义 Tab 图标
- 选中 / 未选中状态
- 图标比例优化
- 更流畅选中动画

---

# 🏗 Architecture | 技术架构

E_Book uses a modular architecture based on **.NET MAUI**.

E_Book 采用模块化架构。

Architecture highlights:

- MAUI Shell navigation
- Modular page system
- Service layer abstraction
- Local metadata storage
- Cross-platform UI design

架构特点：

- MAUI Shell 路由导航
- 模块化页面结构
- Service 层解耦
- 本地数据存储
- 跨平台 UI

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
│   └── SettingsPage
│
├── Services
│   ├── LibraryService.cs
│   └── ReadingMetaStore.cs
│
├── Resources
│   ├── Styles
│   ├── Images
│   └── TabIcons
│
└── AppShell.xaml

```

---

# 🔧 Tech Stack | 技术栈

- **C#**
- **.NET MAUI**
- **SQLite**
- **Shell Navigation**
- **Android Material Components**
- **Preferences API**

---

# 📊 Version Evolution | 版本演进

| Version | Description | 说明 |
|------|-------------|------|
| v1.01 | Base reading system | 阅读基础功能 |
| v1.02 | UI layout refinement | UI 优化 |
| v1.03 | Animated TabBar | 动画 TabBar |
| v1.04 | Settings redesign | Settings 页面 |
| v1.05 | Smart bookshelf system | 智能书架 |
| **v1.06** | UI & navigation improvement | UI 与导航优化 |

---

# 🎯 Design Philosophy | 设计理念

E_Book emphasizes:

- Clean UI design
- Motion-driven interaction
- Structured architecture
- Minimalist purple theme
- Mobile-first experience

E_Book 强调：

- 清晰 UI
- 动效交互
- 结构化架构
- 紫色极简主题
- 移动优先体验

---

# 🛣 Future Roadmap | 后续规划

Planned features:

- Reading analytics dashboard
- Smart book recommendations
- MVVM architecture refactor
- Performance optimization
- iOS UI improvements

未来计划：

- 阅读统计
- 智能推荐
- MVVM 架构
- 性能优化
- iOS UI

---

# 👨‍💻 Developer | 开发者

**Shen Jiawei**  
University of Newcastle  
Mobile Application Development

---

# 📜 License | 许可

This project is developed for **educational purposes**.

本项目用于 **教学与学习用途**。
```
