---

## 📦 更新日志 / Changelog

### 🔖 v1.03  
### 导航结构稳定化与阅读体验优化  
**Navigation Stabilization & Reading Experience Improvement**

---

### 📘 版本概述 / Version Overview

本版本（v1.03）主要针对 v1.02 中暴露出的导航不稳定与页面层级混乱问题进行系统性修复，
重点提升应用在 **Shell 导航结构、阅读页面沉浸体验以及设置模块交互逻辑** 方面的稳定性与一致性。

This release (v1.03) addresses navigation instability and page hierarchy issues identified in v1.02.
The focus is on improving **Shell-based navigation stability, immersive reading experience, and overall consistency of the Settings module**.

---

### ✨ 主要更新内容 / Key Improvements

#### 🧭 导航结构与页面层级  
**Navigation Structure & Page Hierarchy**

- 使用 `RegisterRoute` 重新稳定 Shell 路由机制，避免运行期导航异常  
  Restored stable Shell routing using `RegisterRoute` to prevent runtime navigation issues

- 明确区分主功能页面与子页面，提升整体结构清晰度  
  Clearly separated main functional pages from sub-pages for better structural clarity

- 主 TabBar 页面限定为：
  - **Bookshelf**
  - **Search**
  - **Settings**  
  Main TabBar pages are now limited to Bookshelf, Search, and Settings only

- 设置类、帮助类、阅读类页面通过独立导航进入，不再混入主导航结构  
  Settings, Help, and Reading-related pages are now accessed via dedicated navigation paths

---

#### 📖 阅读页面优化  
**Reading Experience Enhancement**

- 优化 Reading 页面布局，减少干扰元素，提升沉浸式阅读体验  
  Improved the Reading page layout to reduce distractions and enhance immersion

- 改进阅读工具栏与交互逻辑，使操作更加直观  
  Refined the interaction flow of reading controls for improved usability

- 为后续扩展（如阅读进度保存、主题切换等）提供更稳定的页面基础  
  Established a more stable foundation for future reading-related features

---

#### ⚙️ 设置模块改进  
**Settings Module Refinement**

- 重构 Settings 首页结构，使功能入口更加清晰  
  Refined the structure of the Settings home page for clearer navigation

- 优化 Appearance 页面主题切换与“保持屏幕常亮”等功能的呈现方式  
  Improved the presentation of theme switching and “Keep Screen On” options in the Appearance page

- 简化 Help & Support 页面内容，聚焦常见问题与隐私相关信息  
  Streamlined the Help & Support page to focus on FAQs and privacy-related information

---

#### 🛠 稳定性与问题修复  
**Stability & Bug Fixes**

- 修复 v1.02 中出现的多项 XAML 绑定错误  
  Fixed multiple XAML binding issues identified in v1.02

- 解决页面跳转过程中 TabBar 行为异常的问题  
  Resolved TabBar behavior inconsistencies during page navigation

- 清理冗余导航逻辑，减少潜在维护成本  
  Removed redundant navigation logic to reduce future maintenance complexity

---

### 🎓 课程项目说明 / Course Context

该版本体现了 **迭代式开发（Iterative Development）** 的实践过程，
通过分析上一版本的问题并逐步改进系统结构，
符合 **INFT2051 – Mobile Application Development (.NET MAUI)** 课程项目的设计与实现要求。

This version demonstrates an iterative development approach by identifying issues from the previous release and systematically improving the application structure, in alignment with the requirements of **INFT2051 – Mobile Application Development (.NET MAUI)**.

---
