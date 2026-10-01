# KidWall｜童壁
> 专为儿童设计的开源桌面壁纸软件，支持静态图片、动态壁纸，轻量纯净，无广告。

✨ 核心特性
- 🖼️ 静态图片壁纸：本地图片一键设置桌面
- 🎞️ 动态壁纸支持：动画、视频类动态桌面
- 🧒 面向儿童：界面简洁，壁纸资源偏向童趣画风
- ⚡ 轻量低占用：后台资源消耗小，不打扰孩子学习使用电脑
- 🔓 完全开源：可自由二次修改、扩展壁纸源

> 适合家长给孩子电脑配置桌面，自定义孩子喜欢的卡通、星空、插画桌面背景。

## CI/CD：自动发布安装包

推送 `v*` 标签（例如 `v1.0.0`，与 `Directory.Build.props` 的 `<Version>` 一致）会触发 [.github/workflows/release.yml](.github/workflows/release.yml)：先跑全部测试，再分别发布 win-x64 / win-x86 自包含程序，用 Inno Setup 打包简体中文安装向导 `KidWall-v版本-win-x64-setup.exe` / `-win-x86-setup.exe`（附 `.sha256` 校验），最后创建 GitHub Release。也可以在 Actions 页面手动触发并输入版本号。

KidWall 依赖 Windows 切壁纸接口与 Windows 版 VLC 原生库，仅发布 Windows 版本。

本机构建安装包（需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```powershell
dotnet publish src/KidWall.App/KidWall.App.csproj -c Release -f net10.0-windows -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish/win-x64/KidWall.App
./scripts/build_installer.ps1 -Version 1.0.0
```
