## 📦 更新日志 / Changelog

### 🔖 v1.03  
### 导航结构稳定化与阅读体验优化  
**Navigation Stabilization & Reading Experience Improvement**

#### 📘 版本概述 / Version Overview
本版本（v1.03）主要针对 v1.02 中暴露出的导航不稳定与页面层级混乱问题进行系统性修复，重点提升应用在 **Shell 导航结构、阅读页面沉浸体验以及设置模块交互逻辑** 方面的稳定性与一致性。  
This release (v1.03) addresses navigation instability and page hierarchy issues identified in v1.02. The focus is on improving **Shell-based navigation stability, immersive reading experience, and overall consistency of the Settings module**.

---

#### ✨ 主要更新内容 / Key Improvements

**🧭 导航结构与页面层级 / Navigation Structure & Page Hierarchy**
- 使用 `RegisterRoute` 重新稳定 Shell 路由机制，避免运行期导航异常  
  Restored stable Shell routing using `RegisterRoute` to prevent runtime navigation issues
- 主 TabBar 页面限定为：Bookshelf / Search / Settings  
  Main TabBar pages are limited to: Bookshelf / Search / Settings
- 子页面（Reading / Appearance / Help 等）通过独立导航进入，避免混入主导航结构  
  Sub-pages (Reading / Appearance / Help, etc.) are accessed via dedicated navigation paths

**📖 阅读页面优化 / Reading Experience Enhancement**
- 优化 Reading 页面布局，减少干扰元素，提升沉浸式阅读体验  
  Improved Reading page layout to reduce distractions and enhance immersion
- 改进阅读工具栏与交互逻辑，使操作更加直观  
  Refined reading controls and interaction flow for better usability

**⚙️ 设置模块改进 / Settings Module Refinement**
- 重构 Settings 首页结构，使功能入口更加清晰  
  Refined Settings home structure for clearer entry points
- 优化 Appearance 页面主题切换与“保持屏幕常亮”等功能呈现方式  
  Improved theme switching and “Keep Screen On” option presentation
- 简化 Help & Support 页面内容，聚焦常见问题与隐私相关信息  
  Streamlined Help & Support content to focus on FAQs and privacy information

**🛠 稳定性与问题修复 / Stability & Bug Fixes**
- 修复 v1.02 中出现的多项 XAML 绑定与导航相关问题  
  Fixed multiple XAML binding and navigation issues found in v1.02
- 清理冗余导航逻辑，降低后续维护成本  
  Removed redundant navigation logic to reduce future maintenance complexity

---

#### 🎓 课程项目说明 / Course Context
该版本体现了 **迭代式开发（Iterative Development）** 的实践过程，符合 **INFT2051 – Mobile Application Development (.NET MAUI)** 课程项目的实现要求。  
This version demonstrates iterative development and aligns with the requirements of **INFT2051 – Mobile Application Development (.NET MAUI)**.
