> 🎓 **课程作业说明**  
> 本项目为 University of Newcastle 的 **INFT2051 – Mobile Application Development (.NET MAUI)** 课程作业示例，  
> 主要用于展示 .NET MAUI 跨平台应用的基础架构设计、本地数据存储（SQLite）、页面导航以及用户设置与状态持久化等核心概念。

# E-Book 📚

E-Book 是一个基于 **.NET MAUI** 的跨平台电子书阅读应用，支持 Android / iOS / macOS / Windows（受平台与目标框架支持限制）。  
项目提供基础而完整的电子书阅读能力，包括 **阅读设置、阅读进度保存、应用级设置以及 SQLite 本地数据存储**，适合课程项目、学习 .NET MAUI 或作为二次开发的起点。

E-Book is a cross-platform e-book reader built with **.NET MAUI**, targeting Android / iOS / macOS / Windows (subject to platform and target framework support).  
It provides essential e-book reading features such as **reading preferences, progress tracking, app-level settings, and SQLite-based local storage**, making it suitable for coursework, learning, and further extension.

---

## ✨ 功能概览 | Features

### 📖 阅读体验 | Reading Experience
- 字体大小设置并可持久化保存  
- 阅读背景颜色设置并自动恢复  
- 简洁阅读界面，专注内容本身  

- Adjustable font size with persistence  
- Customizable background color with auto-restore  
- Clean and distraction-free reading interface  

### ⏱ 阅读进度 | Reading Progress
- 按书籍/文件记录上次阅读位置  
- 再次打开时自动跳转至上次阅读位置  

- Per-book/file reading progress tracking  
- Automatically resumes from last reading position  

### ⚙️ 应用设置 | App Settings
- 启动密码（可选）  
- 退出锁定（可选）  
- 保持屏幕常亮（可选）  

- Optional startup passcode  
- Optional exit lock  
- Optional keep-screen-on setting  

### 💾 数据与样式 | Data & Styling
- SQLite 本地数据库存储用户设置与阅读进度  
- 使用 ResourceDictionary 统一管理颜色与控件样式  

- SQLite local database for settings and progress  
- Centralized theming via ResourceDictionary  

---

## 🧱 技术栈 | Tech Stack
- **.NET MAUI**
- **C#**
- **SQLite**

---

## 📂 项目结构 | Project Structure

```

E-Book/
├─ Data/                # SQLite 数据库、数据模型、初始化逻辑
├─ Pages/               # 应用页面（阅读页、设置页等）
├─ Resources/
│  ├─ Styles/           # 颜色与控件样式资源
│  └─ Fonts/            # 字体资源
├─ Platforms/           # 平台相关代码（Android / iOS / Windows / macOS）
├─ AppShell.xaml        # 路由与页面注册
└─ MauiProgram.cs       # 应用启动与依赖注册

````

---

## ✅ 环境要求 | Requirements

> ⚠️ 项目使用 .NET 9 目标框架。若仅安装 .NET 8 或缺少 MAUI 工作负载，将无法成功构建。

- **.NET SDK 9.x**
- **.NET MAUI workload** (`maui`)
- Android 运行：Android SDK + Emulator / 真实设备  
- iOS / macCatalyst 运行：macOS + Xcode  
- Windows 运行：仅支持 Windows 系统

### 🔍 验证环境 | Verify Environment
```bash
dotnet --info
dotnet workload list
````

### 🧩 安装 / 修复 MAUI Workload

```bash
dotnet workload install maui
dotnet workload repair
```

---

## 🚀 快速开始 | Getting Started

### 1️⃣ 克隆仓库 | Clone Repository

```bash
git clone https://github.com/Lab3r5/E-Book.git
cd E-Book
```

### 2️⃣ 还原依赖 | Restore Dependencies

```bash
dotnet restore
```

### 3️⃣ 运行 Android | Run on Android

> 需要已启动模拟器或连接真实设备

```bash
dotnet build -t:Run -f net9.0-android
```

### 4️⃣ 运行 Windows | Run on Windows

> 仅可在 Windows 系统上运行

```bash
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

---

## 🗃️ 数据库与存储 | Database & Storage

* **数据库类型**：SQLite

* **存储位置**：应用私有 AppData 目录

* **用途**：

  * 用户应用设置（启动密码、常亮等）
  * 阅读设置（字体大小、背景颜色）
  * 阅读进度（按书籍/文件记录）

* **Database**: SQLite

* **Location**: Platform-specific AppData directory

* **Used for**:

  * App-level settings
  * Reading preferences
  * Reading progress per book/file

数据库在应用首次启动时自动初始化，确保表结构与默认设置可用。

---

## 🛠 常见问题 | Troubleshooting

### ❓ MAUI workload 或 SDK 版本错误

**原因**：未安装 .NET MAUI 工作负载或 SDK 版本不匹配
**解决**：

```bash
dotnet workload install maui
dotnet workload repair
```

---

### ❓ SQLite 报错：`no such table: UserSettings`

**原因**：数据库初始化未完成或旧数据损坏
**解决**：

* 清理应用数据或卸载重装
* 确保数据库初始化方法在查询前已 `await`

---

### ❓ `StaticResource not found`

**原因**：颜色或样式 key 未定义，或资源字典未正确合并
**解决**：

* 检查 `Resources/Styles/Colors.xaml` 中是否包含所有 key
* 确认 `App.xaml` 中已正确合并资源字典

---

### ❓ 字体相关错误

**原因**：不支持的 `FontAttributes` 枚举或字体未注册
**解决**：

* 使用已注册字体族名（如 `OpenSansRegular`）
* 通过 `FontAttributes="Bold"` 实现加粗

---

## 🧭 未来计划 | Roadmap

* [ ] 书架与本地文件导入
* [ ] EPUB / PDF 格式支持
* [ ] 目录、书签与阅读笔记
* [ ] 搜索与文本高亮
* [ ] 云同步与多设备进度同步

---

## 🤝 贡献 | Contributing

欢迎提出 Issue 或 Pull Request。
建议在提交前说明修改内容，并附上必要的截图或说明。

---

## 📄 License

本项目目前未指定 License，仅用于学习与课程作业目的。
如需开源复用，可自行添加 MIT / Apache-2.0 License。

---
