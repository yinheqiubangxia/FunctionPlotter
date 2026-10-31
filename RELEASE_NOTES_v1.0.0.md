# Release v1.0.0 — 跨平台首版

首个公开发布版。MathWorks 仓库历史上第一份覆盖三大平台的版本：Windows 桌面 + Android + 浏览器预览，
另含 Zepp OS / Vela 穿戴版分支。

## 🎉 新增

### Android 平台支持（首次公开 Android 版）
- 全新 `android/` 子项目：Kotlin 2.0 + Jetpack Compose + Material 3
- **Material You 动态配色**：Android 12+ 跟随壁纸取色，旧版本回落到品牌色
- **预测式返回手势**：Compose `BackHandler` + `OnBackPressedDispatcher`，Android 13+ 系统级返回预览
- **多曲线叠加**：可同时画多条函数，自动配色，点 chip 即删
- **AI 辅助绘图**：OpenAI 兼容协议，预设 **DeepSeek** / **Kimi (Moonshot)** / **MiMo (小米)**，支持自定义端点
- **实时预览**：AI 建议在画布上以虚影曲线即时呈现，确认后一键加入
- **响应式布局**：旋转 / 分屏 / 折叠屏下画布与面板自适应，状态不丢失
- 数学引擎与 Windows 桌面版算法等价（分词 → 调度场 → RPN），未定义区间与渐近线正确分段

### Windows 桌面
- AI 自然语言绘图能力整合到 WinUI 3 版本
- 设置面板统一管理多厂商 API Key
- 移除独立的 UWP 子项目（功能已并入 WinUI 3 主线，减少维护负担）

## 🛠 修复

### Android 数学引擎（36 项冒烟测试驱动）
- 调度场算法弹栈循环把左括号 `(` 当运算符查优先级表 → 改用 `PRECEDENCE[top] ?: break`
- RPN 求值 `stack[sp++] = applyFunc(..., sp, ...)` 下标先自增导致函数参数读取错位 → 拆分为两行
- 隐式乘法误将 `|x|` 绝对值管道判为值边界 → 预扫描 Pipe 开/闭状态并跟踪 `pipeOpen`
- `niceStep` 改为向上取 1/2/5（ceil 规则），保证网格步长不过密

## 📦 下载

| 文件 | 说明 |
|---|---|
| `FunctionPlotter-android-v1.0.0-debug.apk` | Android Debug APK，安装即用（仅本地调试，未签名） |
| Source code (zip / tar.gz) | 完整源码 |

> Release APK 未做正式签名；自发布上线应用商店需要重生 keystore 后用 `./gradlew assembleRelease` 重签。

## ✅ 验证

- WinUI 3 桌面：63 项核心自检通过
- Android 数学引擎：36 项冒烟测试通过（用 kotlinc 2.0.20 编译运行）
- Android Debug + Release APK 构建：均成功（AGP 8.7 / Gradle 8.9）

## 🔭 已知限制

- Release APK 未签名（需自行 `apksigner` 处理）
- EnableOnBackPressedCallback 属性非 framework 属性，已移除；预测式返回视觉预览依赖系统 OD 设置（Android 14+ 启用）
- AI 实时预览功能需要用户自备厂商 API Key
- Android UI 仅本机构建验证，未在真机回归

## 📄 许可证

MIT。继续欢迎 Issue / PR。