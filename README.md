# FunctionPlotter · 函数绘图器

<p align="center">
  <img src="docs/screenshot-light.png" width="45%" alt="浅色主题" />
  <img src="docs/screenshot-dark.png" width="45%" alt="深色主题" />
</p>

**输入一个关于 `x` 的表达式，小窗口里立刻画出曲线。**

同一套核心算法覆盖 **Windows 桌面（WinUI 3）** 与 **Android（Jetpack Compose）** 两个平台，另附零依赖的浏览器预览页。
Windows 桌面版纯 XAML 矢量绘制（**不依赖 Win2D**），Android 版纯 Compose Canvas 矢量绘制；深色 / 浅色主题自动跟随系统。

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Windows%2010%201809%2B%20%7C%20Android%208.0%2B-lightgrey)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Kotlin](https://img.shields.io/badge/Kotlin-2.0-7F52FF)

---

## ✨ 特性

- **实时重绘**：每次按键都重新「解析 → 采样 → 绘制」，小窗口一帧仅 1~2 千次求值，毫秒级完成，不做防抖也不掉帧
- **表达式覆盖面广**：四则运算、隐式乘法（`2x`、`(x+1)(x-1)`）、三角 / 双曲 / 指数对数、绝对值 `|x|`、常量 `pi` `e` `tau` `phi`，输入容错（全角括号、`×` `÷` 自动纠正，`f(x) =` 前缀自动忽略）
- **健壮的曲线处理**：`tan(x)`、`1/x` 在渐近线处正确断开，不多画竖线；自适应 Y 轴用分位数而非 min/max，单个尖峰不会把曲线压扁
- **所见即所得的交互**：滚轮 / 双指以光标为中心缩放、拖动平移、双击适配、十字光标实时读数
- **AI 自然语言绘图**：输入「绘制 y=sin(x) 在 -2π 到 2π 的图像」这样的人话，AI 自动转成表达式并画好；预设 **DeepSeek**、**Kimi（月之暗面）**、**小米 MiMo** 三家国产大模型，全部走 OpenAI 兼容接口，可填任意自定义端点
- **多曲线叠加**（Android 版）：同时画多条函数，自动配色
- **一套算法，三个壳**：核心在 `FunctionPlotter.Core`（纯 .NET 类库，零 UI 依赖） / `math/` (纯 Kotlin，零 Android 依赖)，WinUI 3 / Android / 浏览器三个前端共用

## 🚀 快速开始

### Android 版（Kotlin + Jetpack Compose）

见 [`android/`](android/)：Material Design 3 动态配色、预测式返回手势、多曲线叠加、
AI 辅助绘图（DeepSeek / Kimi / MiMo 预设），数学引擎已通过 36 项冒烟测试。

```bash
cd android
./gradlew assembleDebug    # 产物：android/app/build/outputs/apk/debug/app-debug.apk
./gradlew assembleRelease  # 产物：android/app/build/outputs/apk/release/app-release-unsigned.apk（需自签名）
```

> ≥ Android Studio Ladybug，JDK 17，Android SDK 35。

### 0. 浏览器预览（什么都不用装）

直接打开 [`preview/index.html`](preview/index.html)，界面与交互和桌面版等价（同一套算法的 JS 实现）。

### 1. WinUI 3 桌面版（推荐）

前置：Windows 10 1809+，[.NET 8 SDK](https://dotnet.microsoft.com/download)（用 Visual Studio 的话勾选「.NET 桌面开发」）。

```powershell
git clone https://github.com/yinheqiubangxia/FunctionPlotter.git
cd FunctionPlotter
.\build.ps1              # Release / x64，构建完打印 exe 路径
.\build.ps1 -Run         # 构建并直接启动
```

产物在 `publish\x64\FunctionPlotter.exe`。工程已配置自包含（`WindowsAppSDKSelfContained` + `SelfContained`），
**目标机不需要装 .NET 运行时，也不需要 Windows App Runtime**——把 `publish\x64` 整个目录拷走，双击即用。

### 2. 跑测试（63 项核心自检 + 36 项 Android 数学引擎冒烟）

```bash
dotnet run --project tests                                # Windows/.NET 版
kotlinc <math 文件> <smoke test> -include-runtime -d math.jar && java -jar math.jar  # Android 版
```

覆盖表达式解析（运算优先级 / 隐式乘法 / 错误定位）、采样与渐近线断点、自适应 Y 轴、坐标换算、刻度生成等。

## 🤖 AI 自然语言绘图

输入：「绘制 y=sin(x) 在 -2π 到 2π 的图像」「画一个开口向上的抛物线，顶点在 (0, -4)」「高斯函数 exp(-x^2)」
工作方式：自然语言 → 大模型输出严格 JSON → 内置编译器做合法性校验（模型瞎编语法会被拦下）→ 实时重绘。

**内置厂商预设**（均为 OpenAI 兼容接口，填对应 API Key 即可，可在设置里改模型名）：

| 厂商 | 接口地址 | 默认模型 |
|---|---|---|
| DeepSeek（深度求索） | `https://api.deepseek.com` | `deepseek-chat` |
| Kimi（月之暗面 Moonshot） | `https://api.moonshot.cn` | `moonshot-v1-8k` |
| 小米 MiMo | `https://api.xiaomimimo.com/v1` | `mimo-v2.6-flash` |
| 自定义 | 任意 OpenAI 兼容端点 | 自填 |

> Windows 版：API Key 按厂商分别保存在 Windows 密码保险柜（PasswordVault）中。
> Android 版：API Key 仅存储在本机 DataStore，不上传任何第三方。

新增厂商只需在 WinUI 项目 `FunctionPlotter.Core/Ai/LLMProvider.cs` 或 Android 项目 `AiProviders.kt` 里加一条预设即可。

## ⌨️ 交互一览

| 操作 | 效果 |
|---|---|
| 输入框逐字符输入 | 实时重绘 |
| 鼠标滚轮 / 双指捏合 | 以光标 / 双指中心为锚点缩放 |
| 按住左键 / 单指拖动 | 平移 |
| 双击画布 / 「适配」按钮 | 自动适配 Y 轴 |
| `Ctrl+R` / 工具栏「复位」 | 重置到 x ∈ [-10, 10] |
| 移动鼠标（WinUI） | 十字光标 + 读数（x 与 f(x)） |

## 📐 表达式语法

| 类别 | 支持内容 |
|---|---|
| 运算 | `+` `-` `*` `/` `^`（右结合，`-2^2 = -4`）、括号、`\|x\|` 绝对值 |
| 隐式乘法 | `2x`、`3(x+1)`、`(x+1)(x-1)`、`2sin(x)`、`x sin(1/x)` |
| 三角 | `sin` `cos` `tan` `cot` `sec` `csc` `asin` `acos` `atan`，`atan2(y,x)` |
| 双曲 | `sinh` `cosh` `tanh` `asinh` `acosh` `atanh` |
| 指数对数 | `exp` `ln` `log`（默认以 10 为底）、`log(x,b)` 指定底、`log2` `log10`、`pow(x,y)` |
| 其它 | `sqrt` `cbrt` `abs` `sign` `floor` `ceil` `round` `trunc` `frac` `hypot` `max` `min` `mod` |
| 角度 | `rad(deg)` / `deg(rad)` 换算 |
| 常量 | `pi` `π` `e` `tau` `phi` |
| 容错 | 自动忽略 `y =` / `f(x) =` 前缀；全角括号、`×` `÷` `−` 自动纠正 |

输错时给出可读提示并定位，例如 `sin(` → "表达式不完整"，`sinn(x)` → "未知的函数…（是不是 sin？）"。

## 📁 目录结构

```
FunctionPlotter/
├─ FunctionPlotter.sln              .NET 解决方案
├─ build.ps1                        一键构建 WinUI 3 版（Windows）
├─ Directory.Build.props            dotnet CLI 构建时的 VS 打包任务定位
├─ android/                         Kotlin + Jetpack Compose Android 版
│  ├─ build.gradle.kts / settings.gradle.kts
│  └─ app/src/main/java/dev/functionplotter/android/
│     ├─ math/                      纯 Kotlin 表达式解析与采样（零 Android 依赖）
│     ├─ plot/                      ViewModel + Compose Canvas + 手势
│     ├─ ai/                        OpenAI 兼容客户端 + DeepSeek/Kimi/MiMo 预设
│     ├─ ui/theme/                  Material 3 动态配色
│     └─ MainActivity.kt            单 Activity 入口
├─ docs/                            截图
├─ preview/
│  └─ index.html                    浏览器预览版（同一套算法）
├─ rpk/                             Zepp OS 小米手环版（独立 JS 工程）
├─ vela-apps/                       Vela 快应用三件套（绘图器/番茄钟/换算器，独立 JS 工程）
├─ src/
│  ├─ FunctionPlotter.Core/         纯 .NET 类库（表达式解析 / 采样 / 坐标换算）
│  │   └─ Ai/                       AI 层：LLMProvider 预设、AiPlotService（OpenAI 兼容调用）、MiniJson
│  └─ FunctionPlotter.WinUI/        WinUI 3 应用（含 AI 自然语言绘图）
└─ tests/                           63 项核心自检（跨平台）
```

## 🧰 构建要求

| 目标 | 要求 |
|---|---|
| Android | JDK 17，Android SDK 35，AGP 8.7，Gradle 8.9 |
| WinUI 3 | Windows 10 1809+，.NET 8 SDK（或 VS 2022 17.8+ 勾选「.NET 桌面开发」） |
| 测试 | 任意平台，`dotnet run --project tests` |

> 说明：WinUI 3 的 PRI 打包任务只随 Visual Studio 分发，因此命令行构建需要本机装有 VS（`build.ps1` 会自动定位）；
> 仅用 VS IDE 打开构建则无此要求。

## 🤝 贡献

Issue / PR 都欢迎。改核心算法请先跑 `dotnet run --project tests`，保证 63 项自检全绿；
改 Android 版块请运行 `MathSmokeTest`（36 项）。

## 📄 许可证

[MIT](LICENSE) © 2026 yinheqiubangxia