---
name: wintweaker-release
description: >-
  Packages WinTweaker customer distribution (light framework-dependent + full
  self-contained), syncs HelpPage/使用教程 with latest features, verifies
  artifacts, and smoke-checks. Use when the user asks to 发布, 打包,
  package-dist, dist, 轻量版, 完整版, customer package, release WinTweaker,
  更新教程, or 更新帮助页 before shipping.
---

# WinTweaker 客户发布

项目根：仓库根目录。作者微信恒为 `zxzvsdcj`。

## 何时使用

用户说「发布 / 打包 / 出客户包 / package-dist / 轻量版+完整版 / 更新帮助或使用教程后再发」时执行本流程。  
**禁止**只 `dotnet build` 或只 publish 却跳过文档同步。

## 硬门禁：文档必须与功能同步

每次发布前，Agent **必须**对照当前 UI/功能清单，更新两处文档后再打包：

| 文档 | 路径 | 受众 |
|------|------|------|
| 应用内帮助 | `WinTweaker/Views/HelpPage.xaml` | 已安装用户 |
| 客户教程 | `WinTweaker/使用教程.md` | 随包分发 |

同步规则：

1. 本轮或未发布过的**新功能 / 改文案 / 改交互**（开关、确认框、重启要求、置灰条件）→ 两处都要写到。
2. 帮助页「常规优化 / 更新管理 / Win11 / 高危」条目与界面列表一致；教程表格与帮助页一致，可更细。
3. 高危或需重启项必须写清风险与生效方式（例：HVCI 需重启电脑；UAC 需重启）。
4. `scripts/package-dist.ps1` 含 **Docs sync gate**：帮助页与教程须同时命中关键词（当前至少：`内存完整性`、`导出方案|配置方案`、`zxzvsdcj`）。未通过则打包失败——应改文档，勿删门禁。
5. 新增重要功能时：同步扩展 `package-dist.ps1` 的 `$requiredDocHints`，防止下次漏更。

文档更新检查清单（发布前勾选）：

```
文档同步:
- [ ] 扫 General / Update / Win11 / Danger / Help 页，列出相对上一版客户包的差异
- [ ] 更新 HelpPage.xaml 对应章节
- [ ] 更新 使用教程.md 对应表格/排障
- [ ] 若有新「必写」功能，加入 package-dist Docs gate 关键词
- [ ] 再执行打包
```

## 产物约定

| 产物 | 路径 | 说明 |
|------|------|------|
| 轻量版目录 | `dist/轻量版/` | 需 .NET 9 Desktop Runtime x64；exe 约数 MB |
| 完整版目录 | `dist/完整版/` | 自包含免运行时；exe 约 55–80MB |
| ZIP | `dist/WinTweaker-轻量版.zip`、`dist/WinTweaker-完整版.zip` | 目录压缩包 |

每目录必须含：`WinTweaker.exe`、`使用教程.md`、`请先阅读.txt`。不得含 `license_config.json` 或 `.pdb`。凭证只在构建期从 gitignore 的 `license_config.json` 编进 exe。

## 标准发布步骤

```
发布进度:
- [ ] 0. 文档同步（HelpPage + 使用教程 + 必要时 Docs gate）
- [ ] 1. 确认构建输入 `WinTweaker\license_config.json` 存在（gitignore，不进 dist）
- [ ] 2. 运行 package-dist.ps1
- [ ] 3. 核对 dist 产物与体积（完整版 exe > 轻量版）
- [ ] 4.（可选）verify-dist.ps1
- [ ] 5. 回报路径与体积；不自动 git commit / push
```

### 0–1. 密钥与文档

```powershell
Test-Path .\WinTweaker\license_config.json
```

缺失则 WARN（客户无法激活）；暂停让用户补齐，**永不提交**该文件。

### 2. 一键打包

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-dist.ps1
```

流程：Docs gate → 清空 `dist/` → Light → Full → 复制教程/说明/license → ZIP。

排障用：

```powershell
dotnet publish .\WinTweaker\WinTweaker.csproj -c Release -p:PublishProfile=Light -o .\dist\轻量版
dotnet publish .\WinTweaker\WinTweaker.csproj -c Release -p:PublishProfile=Full -o .\dist\完整版
```

Profile：`WinTweaker/Properties/PublishProfiles/Light.pubxml`、`Full.pubxml`。

### 3. 验收

必须存在：两目录下的 exe / 使用教程.md / 请先阅读.txt，以及两个 ZIP。  
完整版 exe **大于** 轻量版。

可选：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-dist.ps1
```

（会再打包并尝试管理员启动 GUI。）

### 4. 交付话术

- 轻量版：已装 .NET 9 Desktop Runtime。
- 完整版：开箱即用。
- 启动：右键「以管理员身份运行」。
- 定制：微信 `zxzvsdcj`。

## 本地脚本边界

`scripts/`、`dist/` 在 `.gitignore`。客户分发以 `package-dist.ps1` → `dist/` 为准，不以根目录 `publish-light` / `publish-full` 为准。

| 脚本 | 用途 |
|------|------|
| `scripts/package-dist.ps1` | 文档门禁 + 客户双包 + ZIP |
| `scripts/verify-dist.ps1` | 发布验收 + 启动 Debug |
| `build.ps1 -Mode Both` | 仅工程发布目录（非客户包布局） |

## 禁止事项

- 跳过 HelpPage / 使用教程 更新就打包给客户。
- 自动 `git commit` / `push`（除非用户明确要求）。
- 把 `license_config.json` 密钥写入 Skill、记忆或聊天。
- 修改作者信息偏离 `微信号：zxzvsdcj`。
- 为通过门禁而删除 Docs gate（应补文档或扩展关键词）。
