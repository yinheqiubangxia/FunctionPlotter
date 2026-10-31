# FunctionPlotter for Android · 函数绘图器

基于 **Kotlin + Jetpack Compose** 与 **Material Design 3** 的 Android 函数绘图器，
是 [FunctionPlotter Windows 版](../README.md) 的移动端姊妹项目。

输入一个关于 `x` 的表达式，立刻画出曲线；也可以直接对 AI 说「画个心形线」。

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![Platform](https://img.shields.io/badge/platform-Android%208.0%2B-green)
![Kotlin](https://img.shields.io/badge/Kotlin-2.0-7F52FF)

---

## ✨ 功能特性

### 绘图引擎
- **表达式覆盖面广**：多项式、四则运算、幂（`^`）、隐式乘法（`2x`、`(x+1)(x-1)`）、
  三角 / 反三角 / 双曲、`exp` `ln` `log` `lg` `sqrt` `abs` `floor` `ceil` 等，
  常量 `pi` `e` `tau` `phi`，绝对值 `|x|`，输入容错（全角括号、`×` `÷`、`f(x)=` 前缀自动纠正）
- **多条曲线**：可同时叠加绘制多条函数，每条自动配色，点 chip 即删
- **无定义区间识别**：`ln(x)` 在 `x ≤ 0`、`1/0` 等处自动断段，不画假线
- **渐近线分段**：`tan(x)`、`1/x` 通过「中点回溯验证 + 屏幕跳变保护」双重判定，
  极点两侧正确断开，不会在屏幕上甩出贯穿竖线
- **自适应采样**：按视口宽度动态调整采样密度（像素宽 × 1.5，上限 4096 点），
  缩放到任何量级都平滑

### 交互
- 单指拖动**平移**，双指捏合以手势中心为锚点**缩放**，双击**复位视图**
- 网格 / 坐标轴一键开关，网格步长自动取 1/2/5 × 10ⁿ 的「好看」数值并附标签

### 界面（Material Design 3）
- **动态配色**（Material You）：Android 12+ 跟随壁纸取色，旧版本回落到品牌色
- 明暗主题自动跟随系统；边到边沉浸式布局
- **预测式返回手势**（Predictive Back）：面板打开时返回手势有系统级预览动画
- 响应式布局：旋转 / 分屏 / 折叠屏下画布与面板自适应，状态不丢失
- 所有过渡使用 Material 3 标准动效（BottomSheet、Snackbar、FAB）

### AI 辅助绘图
- 自然语言 → 表达式：「x 的三次方减 3x」「画个心形线」
- **实时预览**：AI 建议在画布上以虚影曲线即时呈现，点击建议可切换预览，确认后一键加入画布
- **可配置 API**：OpenAI 兼容协议，预设 **DeepSeek**、**Kimi（Moonshot）**、**MiMo（小米）**，
  也可填任意自定义兼容端点（Base URL / 模型均可改）
- API Key 仅存储在本机 DataStore，不上传任何第三方

---

## 🏗 项目结构与模块划分

```
android/
├── settings.gradle.kts / build.gradle.kts / gradle.properties   # 构建配置
├── app/
│   ├── build.gradle.kts          # Compose BOM、OkHttp、DataStore 依赖
│   └── src/main/
│       ├── AndroidManifest.xml   # enableOnBackPressedCallback="true"（预测式返回）
│       ├── res/                  # 自适应图标、主题、字符串
│       └── java/dev/functionplotter/android/
│           ├── MainActivity.kt        # 单 Activity + 全部 Compose 界面
│           │                          # （输入栏、曲线 chips、AI 面板、设置面板）
│           ├── math/
│           │   ├── ExpressionParser.kt   # 分词 → 调度场算法 → RPN，纯 Kotlin 零依赖
│           │   └── Sampler.kt            # 采样、NaN 断段、渐近线检测、网格步长
│           ├── plot/
│           │   ├── PlotViewModel.kt      # 视口/曲线列表/采样结果状态管理
│           │   └── PlotCanvas.kt         # Compose Canvas 绘制 + 手势处理
│           ├── ai/
│           │   ├── AiConfig.kt           # 服务商预设 + DataStore 持久化
│           │   ├── AiService.kt          # OpenAI 兼容 chat/completions 客户端
│           │   └── AiViewModel.kt        # AI 请求与预览状态
│           └── ui/theme/Theme.kt         # M3 动态配色 / 明暗主题
└── LICENSE                       # MIT
```

### 关键实现思路

1. **表达式解析**（`ExpressionParser`）
   经典「分词 → Shunting-yard → RPN」三段式。求值时不抛异常，未定义一律返回 `NaN`，
   把「哪里画不出来」的决策权交给采样层。隐式乘法在分词后做一次 token 级插入，
   `2x`、`x sin(x)`、`(x+1)(x-1)` 都自然成立。

2. **断点与渐近线**（`Sampler.isAsymptote`）
   相邻采样点函数值异号且量级巨大时，取**区间中点回溯求值**：
   中点值若落在两端范围之外（真极点的特征），判定跨渐近线并断段；
   否则（如 `x²` 的陡坡）照常连线。绘制层再加一道「屏幕跳变 > 4 倍高度则 moveTo」的保险。

3. **视口变换**（`Viewport`）
   世界坐标 ↔ 屏幕坐标由 `centerX/centerY/unitsPerPixel` 三元组描述，
   缩放保持锚点下世界坐标不动（双指缩放不漂移）。

4. **AI 链路**
   System Prompt 约束模型只输出 `{"expressions": [...], "note": "..."}`，
   解析后经同一个 `ExpressionParser.validate` 校验再进预览，坏表达式永远不会上画布。

---

## 🚀 构建与运行

前置：**Android Studio Ladybug+**（自带 JDK 17 与 Android SDK），或命令行准备 JDK 17 + Android SDK 35。

### Android Studio（推荐）
1. `Open` 选择 `android/` 目录，等待 Gradle Sync 完成
2. 首次同步后如需命令行构建，可先执行一次 `gradle wrapper` 生成 `gradlew`
3. 连接设备或启动模拟器（API 26+），点 **Run ▶**

### 命令行
```bash
cd android
gradle wrapper          # 首次：生成 gradlew（本仓库不提交 wrapper jar）
./gradlew assembleDebug # 构建，产物在 app/build/outputs/apk/debug/
./gradlew installDebug  # 安装到已连接设备
```

Release 构建（已开启 R8 压缩与资源瘦身）：
```bash
./gradlew assembleRelease
```

### 配置 AI（可选）
应用内 → 右下角 ✨ FAB → 右上角设置图标 → 选择服务商并填入 API Key：
| 服务商 | 获取 Key |
|---|---|
| DeepSeek | platform.deepseek.com |
| Kimi | platform.moonshot.cn |
| MiMo | MiMo 开放平台 |

不配置 AI 不影响任何绘图功能。

---

## 🛠 技术栈

| 层 | 选型 |
|---|---|
| 语言 | Kotlin 2.0 |
| UI | Jetpack Compose + Material 3（动态配色、预测式返回） |
| 架构 | 单 Activity + MVVM（StateFlow） |
| 绘图 | Compose Canvas（矢量 Path，无第三方图形库） |
| 网络 | OkHttp + org.json（AI 功能专用） |
| 持久化 | DataStore Preferences（仅 AI 配置） |
| minSdk / targetSdk | 26 / 35 |

## 可维护性与扩展性

- `math/` 为**纯 Kotlin 模块**，零 Android 依赖，可直接搬到桌面版或做单元测试
- 新增服务商：在 `AiProviders` 里加一条预设即可，无需动网络层
- 新增函数：在 `ExpressionParser.FUNCTIONS` 与 `applyFunc` 各加一行
- 绘图层只消费 `List<Segment>`，未来换渲染后端（OpenGL / 离屏位图）不影响数学层

## 📄 许可证

[MIT](LICENSE)
