# 贡献指南

感谢你愿意改进 SkyMatch。这个项目是一个 Windows / WinForms / .NET Framework 4.8 的桌面应用，代码风格偏保守，优先保证可运行、可回滚和低资源占用。

## 先讨论，再大改

- Bug、UI 建议、算法调整：先开 Issue，说明使用场景与复现步骤。
- 大功能 / 重构 / 更换模型：先在 Issue 里讨论，避免提交后因许可证或性能问题被回退。
- 不要在 Issue 里粘贴完整 `settings.json`、日志中的本机路径、Steam 凭据或未脱敏截图。

## 开发环境

```powershell
git clone https://github.com/Trzipx/SkyMatch.git
cd SkyMatch
.\scripts\import-models.ps1 -PortableZip .\SkyMatch-1.2.0-Portable.zip
.\build.cmd
.\dist\SkyMatch.exe
```

- Windows 10 / 11
- .NET Framework 4.8（系统自带或 Developer Pack）
- PowerShell 5.1+
- 可选：Wallpaper Engine（联调切换 / 音频）

## 提交前检查

- [ ] `.\build.cmd` 成功，且没有新增编译警告（如可避免）
- [ ] 手动测试受影响的页面：首页 / 壁纸库 / 工坊 / 设置
- [ ] 不提交 `dist/`、`release/`、`models/`、`backups/`、`.tools/`、用户缓存、截图中的真实位置
- [ ] 新增第三方代码 / 模型时，同步更新 `THIRD_PARTY_NOTICES.md`
- [ ] 涉及自动切换的修改，说明全屏 / 已匹配 / 手动切换的行为
- [ ] 涉及 Steam 抓取的修改，说明失败回退与限流处理

## 代码风格

- C# 5 语法（项目使用 `csc /langversion:5`），不要使用字符串插值、`?.`、`async` 之外的新语法。
- 保持现有命名与缩进；新增 public 成员尽量少。
- WinForms 控件自绘优先复用 `Theme`，不要在业务代码里硬编码颜色。
- 网络 / 识图 / 文件 IO 不要阻塞 UI 线程。
- 新增用户可见文字使用中文；日志可中英混合，但不要输出敏感信息。

## 算法改动

- 说明改动的目标指标（误判率、切换次数、首屏耗时、内存等）。
- 不要为了让某一张壁纸通过而写死标题 / ID。
- 修改评分权重时，给出一个可复现的候选集和前后对比。
- 更换模型时先确认许可证与本项目 AGPL-3.0 兼容。

## Pull Request

1. Fork 并从 `main` 开分支；
2. 一个 PR 只做一件事；
3. 写清楚“改了什么、为什么、怎么验证”；
4. 附截图 / 日志时先脱敏；
5. 维护者可能会要求拆小或补测试。

## 许可证

提交代码即表示你同意以 **AGPL-3.0** 授权你的贡献。
