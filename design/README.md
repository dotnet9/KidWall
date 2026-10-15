# KidWall 产品原型

## 单文件交付

`design/` 中的主 HTML 已内嵌样式、脚本、图标和图片。只复制这一个 HTML 到空目录即可离线查看并操作，无需旁边的资源文件或本地服务。

维护源码放在 `scripts/prototype/`；执行 `pwsh scripts/build_standalone_prototype.ps1` 重新生成，`-Check` 校验生成文件。不要直接修改交付 HTML。交互审计会复制 HTML 到空目录，阻止其他文件和网络资源请求，并检查脚本错误与图片完整性。
