# 📚 E_Book

> A modern cross-platform e-book reader built with **.NET MAUI**  
> 基于 **.NET MAUI** 构建的现代跨平台电子书阅读应用

---

# 🏷 Version | 版本信息

![Version](https://img.shields.io/badge/version-v1.09-purple)
![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-blue)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub stars](https://img.shields.io/github/stars/Lab3r5/E_Book)

**Current Version:** `v1.09 – Account Security & Password Recovery System`  
**当前版本：** `v1.09 – 账号安全系统与密码找回功能`

---

# ✨ Project Overview | 项目简介

**E_Book** is a cross-platform mobile reading application developed with **.NET MAUI**.

The goal of the project is to build a **modern mobile reading experience** with:

- Clean UI design  
- Structured application architecture  
- Lightweight local storage  
- Smooth reading interactions  
- Realistic account system  

E_Book 是一个基于 **.NET MAUI** 构建的跨平台移动阅读应用，目标是打造：

- 现代化阅读界面  
- 清晰结构化架构  
- 轻量级本地数据存储  
- 流畅阅读体验  
- 接近真实产品的账号系统  

Developed for:

**INFT2051 – Mobile Application Development**  
University of Newcastle

---

# 🚀 Latest Release – v1.09
# 最新版本 – v1.09

Version **v1.09** introduces a **complete account security system**, including password recovery and structured authentication features.

v1.09 版本引入了完整的账号安全体系，包括密码找回与认证流程升级，使应用更加接近真实商业产品。

Key improvements include:

• Forgot Password workflow  
• Security Question system (Picker-based)  
• Reset Password page  
• Navigation system upgrade  
• Privacy Policy update  
• Usage Guidelines enhancement  

该版本重点提升：

• 忘记密码功能  
• 安全问题机制（下拉选择）  
• 重置密码页面  
• 导航结构升级  
• 隐私政策更新  
• 使用指南完善  

---

# 🔐 Account Security System
# 账号安全系统

## Forgot Password 功能

Users can securely reset their password through:

用户可以通过以下流程找回密码：

1. Enter email  
2. Answer security question  
3. Set new password  

该功能完全基于本地实现，无需服务器支持。

---

## Security Question（安全问题）

Security questions are now **predefined instead of free input**.

安全问题改为固定选项，提高系统一致性：

- What is your favorite pet's name?  
- What is your favorite book?  
- What city were you born in?  

优势：

- 更安全  
- 更规范  
- 更接近真实产品设计  

---

## Reset Password Page

A dedicated page for password reset with:

- Input validation  
- Error feedback  
- Smooth UI interaction  

提供完整用户体验。

---

# ⭐ Key Features | 核心功能

## 📚 Smart Bookshelf System
## 智能书架系统

- Reading progress display  
- Continue reading indicators  
- Recently opened sorting  
- Automatic book cover generation  

---

## 👤 Multi-User Account System
## 多用户账号系统

Each user has independent:

- Library  
- Reading progress  
- Settings  
- Search history  

账号之间完全隔离。

---

## 👤 Guest Mode
## Guest 模式

- Independent storage profile  
- No impact on registered users  

---

## 📖 Reading Engine
## 阅读系统

Supported formats:

- TXT  
- EPUB  
- DOCX  
- RTF  
- PDF  

Features:

- Font size control  
- Theme switching  
- Progress saving  
- Resume reading  

---

# 🎨 UI & UX Improvements | 界面优化

Version **v1.09** enhances UI consistency and interaction flow:

- Unified authentication pages (Login / SignUp / Forgot Password)  
- Picker-based input interaction  
- Improved feedback animations  
- More consistent design language  

整体界面更加接近商业级应用。

---

# ⚡ Performance Optimization
# 性能优化

- Faster navigation transitions  
- Optimized bookshelf loading  
- Reduced redundant refresh  
- Improved UI responsiveness  

---

# 🏗 Architecture | 技术架构

- MAUI Shell navigation  
- SQLite local database  
- Preferences API  
- Service layer design  
- User-based data isolation  

---

# 📂 Project Structure | 项目结构

```

E_Book
│
├── Models
├── Pages
│   ├── LoginPage
│   ├── SignUpPage
│   ├── ForgotPasswordPage
│   ├── ResetPasswordPage
│   ├── BookshelfPage
│   ├── ReadingPage
│   ├── SearchPage
│   ├── SettingsHomePage
│
├── Services
├── Data
│   └── Database.cs
│
├── Resources
└── AppShell.xaml

````

---

# 🔧 Tech Stack | 技术栈

- C#  
- .NET MAUI  
- SQLite  
- Shell Navigation  
- Preferences API  

---

# 📦 Installation | 安装方式

```bash
git clone https://github.com/Lab3r5/E_Book.git
````

Open in:

```
Visual Studio 2022+
```

Run on:

* Android Emulator
* Android Device

---

# 📊 Version Evolution | 版本演进

| Version   | Description                          |
| --------- | ------------------------------------ |
| v1.01     | Base reading system                  |
| v1.02     | UI layout refinement                 |
| v1.03     | Animated TabBar                      |
| v1.04     | Settings redesign                    |
| v1.05     | Smart bookshelf                      |
| v1.06     | UI & navigation improvements         |
| v1.07     | Multi-user data isolation            |
| v1.08     | Settings UI refinement               |
| **v1.09** | Account security & password recovery |

---

# 🎯 Design Philosophy | 设计理念

* Clean UI
* Smooth interaction
* Structured architecture
* Minimalist design
* Real-world product alignment

---

# 🛣 Future Roadmap | 后续规划

* Reading analytics dashboard
* Smart recommendations
* MVVM architecture
* iOS support

---

# 👨‍💻 Developer | 开发者

**Shen Jiawei**
University of Newcastle

---

# 📜 License | 许可

This project is developed for **educational purposes**.
