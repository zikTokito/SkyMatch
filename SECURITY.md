# 安全策略

## 报告漏洞

请不要在公开 Issue 中发布可利用细节。

优先使用 GitHub 的 **Private vulnerability reporting**（仓库 → Security → Report a vulnerability）。如果该入口不可用，请开一个不含利用代码的 Issue，说明“需要私下联系”。

请尽量提供：

- 受影响的版本 / 提交；
- 复现步骤；
- 影响范围（本地提权、任意文件写入、网络请求伪造等）；
- 你建议的修复方向（可选）。

## 支持范围

只维护最新 Release 对应的源码。旧版本 / 自行修改的版本不保证修复。

## 已知边界

- SkyMatch 会读取 Wallpaper Engine 的本地配置和壁纸目录，并写入 `%LOCALAPPDATA%\SkyMatch`；这是设计行为。
- 工坊抓取使用公开 Steam 页面，不携带你的 Steam 登录 Cookie。
- 模型推理完全本地；不会上传壁纸或桌面截图。
- 项目没有自动更新器，不会自行下载并执行远程二进制。
- 安装程序未做代码签名，Windows SmartScreen 可能提示“未知发布者”。

## 不要提交的内容

- `%LOCALAPPDATA%\SkyMatch` 下的用户设置 / 缓存 / 日志；
- Steam 凭据、Cookie、令牌；
- 含真实位置、账号名、桌面内容的截图；
- 模型权重或来源不明的二进制。
