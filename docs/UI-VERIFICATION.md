# 原型与实现验收

2026-10-15，版本 1.0.8。原型位于 `design/index.html`，产品使用 Semi、Ursa 与 CodeWF 控件体系。以相同的 1280 × 820 窗口核对标题栏、分类、四列图库、设置和关于窗口；不会用构建成功代替界面验收，也不把截图数量折算成还原百分比。

## 已验证

- 核心测试 8 项通过。偏好、壁纸目录均使用源生成 JSON。
- 实际界面 268 条断言，覆盖真实控件绑定、持久化、动态切语与更新状态；Windows NativeAOT 使用同一套断言运行。
- 收藏与本地目录使用稳定标识写入偏好，覆盖刷新、重复目录规范化、重启恢复、移除收藏后的即时过滤及计数通知；增加收藏与本地分类。原型同步收藏、唯一当前壁纸、跨刷新状态和翻译后的分类搜索。
- 标题栏只保留八个配色点、Ursa 三态主题按钮及 Semi 语言菜单，设置页去掉重复外观入口；实际点击 Ursa 内部按钮并通过真实 ListBox 验证选择与持久化。
- 实际展开语言菜单并执行菜单项；Ursa 内部按钮的三态工具提示绑定宿主 T4 资源，跟随中英切换即时刷新。
- 真实 XAML 离屏回归覆盖八套配色的浅深模式，另验证系统模式、模式与配色独立保存、搜索、预览、收藏和当前卡片动态切语。
- 关于窗口保持打开时切换中英，校验实际程序集版本、编译时间和平台。受控服务验证检查、失败、下载后安装入口、取消和关闭取消；不启动真实安装器。
- 静态图库使用仓库 12 张真实图片及隔离偏好。浅深两种模式下检查所有可见徽标，装饰光效不能产生“动态壁纸”徽标。
- 浏览器审计覆盖两语言、三种主题模式、八套配色、关于、受控更新状态和脚本异常；原型与应用 JSON 由脚本保持同步。
- Windows 仓库发布脚本及原生离屏验证分别执行。使用隔离目录及壁纸替身，验收不会设置用户的桌面壁纸。

```powershell
dotnet test --project tests/KidWall.Core.Tests/KidWall.Core.Tests.csproj -c Release
pwsh scripts/sync_localization_keys.ps1 -Check
dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release -- artifacts/ui-verification
npm ci --prefix design/tests
# 首次在 design/tests 中运行 npx playwright install chromium
npm test --prefix design/tests
pwsh scripts/publish.ps1 -RuntimeIdentifier win-x64
dotnet publish scripts/ui-verification/UiVerification.csproj -c Release -r win-x64 -p:PublishAot=true -p:IlcGenerateCompleteTypeMetadata=true -p:IlcTrimMetadata=false -p:IlcSingleThreaded=true -o artifacts/native-ui
./artifacts/native-ui/UiVerification.exe artifacts/native-ui-captures
```

## 对照与差异

| 检查点 | 实现及原型状态 |
| --- | --- |
| 标题栏、搜索、分类、卡片 | 保留同一功能层级，60 px 标题栏、1240 px 内容上限、20 px 卡片间距；平台窗口按钮使用 CodeWFWindow |
| 配色及语言 | 同源八色令牌、三种独立模式、中英文选择，宿主资源输出，库资源内嵌 |
| 设置与关于 | 独立关于窗口；未实现的省电和内容过滤均明确禁用 |
| 动态徽标 | 只由壁纸媒体类型决定；静态卡片的星光等装饰效果不会显示动态徽标 |
| 图库内容 | 原型有 16 个示例；产品加载仓库及用户选定目录；回归图库有 12 张静态图。条目数量和动态媒体不能按截图等同 |
| 更新 | 原型仅模拟状态；产品使用 Toolkit 的实际检查、下载和安装服务 |

![原生关于窗口](media/kidwall-native-about.png)

当前尚不能宣称达到全页面 95% 还原：真实视频播放器、浮层过渡的逐帧视觉、多 DPI、键鼠及输入法尚未逐项桌面验收。锁屏期间使用真实控件离屏渲染；未重新操作实际桌面或执行真实安装。Linux/macOS 运行、Windows 壁纸设置和 VLC 动态桌面仍需对应环境与用户数据下的专项验收。原生编译存在依赖库的裁剪和 AOT 分析警告，已通过路径不代表全部反射分支。

本轮最终原生验收：共享 Avalonia 12.1.4.21，正式 Windows x64 NativeAOT 发布与260项实际界面断言通过。修正卡片遮罩40%透明停靠点后重新发布并复验通过；本页及README的PNG来自最终NativeAOT验证器。Windows EXE安装包已编译，签名/DMG失败传播、五平台安装包与SHA256门禁正反例已验证，实际安装器交互未执行。
