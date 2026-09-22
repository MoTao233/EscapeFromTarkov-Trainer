# 部署说明（SPT / BepInEx 环境）

本 fork 在 SPT 环境下由 **三个文件**组成，缺一不可：

| 文件 | 位置 | 作用 |
|---|---|---|
| `NLog.EFT.Trainer.dll` | `<游戏>\EscapeFromTarkov_Data\Managed\` | trainer 本体 |
| `trainer-highlights` | `<游戏>\EscapeFromTarkov_Data\` | 人物/物品描边 Shader 资源包 |
| `spt-efttrainer.dll` | `<游戏>\BepInEx\plugins\` | 加载入口，BepInEx 拉起它调用 `Loader.Load()` |

## 注意

- **NLog 加载方式已失效**：EFT ≥ 0.13.0.21531 后游戏禁止通过 `NLog.dll.nlog` 加载扩展 target，不要再手动创建该文件。上游安装器在检测到 BepInEx 时也只走插件方式。
- 依赖项（整合包/SPT 自带，无需处理）：`BepInEx\core\0Harmony.dll`、`EscapeFromTarkov_Data\outline`。
- 开菜单热键默认值在 `Features\Commands.cs` 的 `Key` 属性（当前为 `Insert`），改键后需重新编译部署。

## 构建 + 部署流程

```powershell
# Shader 改动后先编译资源并进行图形检查
powershell -File Build-Shaders.ps1 -Test

# 1. 编译 trainer 本体并打包到 artifacts\visible-esp-zh-cn
powershell -File Build-Local.ps1 -GamePath "E:\EFT\SPT_3114"

# 2. 部署 trainer DLL 和 trainer-highlights（自动备份、校验哈希、失败回退）
powershell -File artifacts\visible-esp-zh-cn\Install-Local.ps1 -GamePath "E:\EFT\SPT_3114"

# 3. 编译 BepInEx 插件（PostBuild 事件会自动拷贝到 BepInEx\plugins）
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
    BepInExPlugin\BepInExPlugin.csproj -t:Rebuild -nologo -verbosity:minimal `
    -p:Configuration=Release -p:EFTBasePath=E:\EFT\SPT_3114
```

部署前确保游戏已关闭，部署后重启游戏生效。

## 排错

按热键无反应时，按顺序检查：

1. 两个 DLL 和 trainer-highlights 是否都在对应位置（对照上表）
2. `BepInEx\LogOutput.log` 里搜 `efttrainer` / `Exception` —— 看插件是否加载、初始化是否报错
3. `Managed\` 里不应存在 `NLog.dll.nlog`（该机制对本版本无效，残留文件可删）
4. 游戏版本须为 `0.16.1.35392`（`Install-Local.ps1` 会校验）

菜单正常但人物和物品都不高亮时，检查游戏根目录 `trainer-highlights.log`（MO2 启动也检查其 overwrite）。日志包含 Shader 加载错误和每 5 秒一次的候选/绘制统计：`players/items` 为零说明没有收集到目标；有候选却 `selected/draws` 为零说明被筛选；有绘制次数却不可见则继续查相机输出。新版事件应为 `BeforeForwardAlpha`，保证进入游戏的后处理输入。退出游戏后的旧 `AssetBundle.Unload` 异常已修正，不应把其他插件的异常当作高亮故障依据。

## 开发环境备忘（本机）

- Visual Studio 2026 Community + ".NET Framework 4.7.1 目标包"（主项目是 net471 旧格式，必须 MSBuild 编译）
- .NET SDK（Installer 项目 / `dotnet tool restore` / `dotnet format`）
- PowerShell 执行策略已设为 `RemoteSigned`（CurrentUser）；本机 `Microsoft.PowerShell.Security` 模块损坏属 Windows bug，`Set-ExecutionPolicy`/`Get-ExecutionPolicy` 不可用，与构建无关
- Git：`safe.directory` 需加 `E:/EFT/EscapeFromTarkov-Trainer`；连 GitHub 走代理 `http.https://github.com.proxy http://127.0.0.1:7897`

人物及物品描边配置、性能筛选与回退步骤见 [VISIBLE-ESP.md](VISIBLE-ESP.md)。
