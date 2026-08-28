# Win-Tweaker

Windows 10/11 全版本系统开发者优化工具，提供系统性能调优、隐私保护、更新管理等功能。

## 功能概览

### 常规优化

| 功能 | 说明 |
|------|------|
| 卓越性能电源计划 | 启用系统隐藏的 Ultimate Performance 电源计划 |
| 系统遥测降级 | 将诊断数据级别设为基础级，减少后台上传 |
| 关闭系统广告 | 禁用锁屏/开始菜单/通知/同步等全部广告推送 |
| 裁剪冗余服务 | 禁用 DiagTrack、Xbox 全套、SysMain 等服务 |
| 全局禁止后台运行 | 阻止 UWP/商店应用在后台消耗资源 |
| 关闭休眠 | 删除 hiberfil.sys 释放 C 盘空间（保留睡眠） |
| 资源管理器优化 | 关闭云广告、强制显示文件扩展名 |
| WSL2 高性能配置 | 自动生成优化的 .wslconfig 配置文件 |

### 更新管理

| 功能 | 说明 | 实现机制 |
|------|------|----------|
| 禁止 Edge 自动更新 | 阻止 Microsoft Edge 后台自动更新 | 策略注册表 + EdgeUpdate 服务禁用 |
| 禁止 Chrome 自动更新 | 阻止 Google Chrome 后台自动更新 | Google Update 策略（32/64位注册表）+ 服务禁用 |

### Win11 专属

| 功能 | 说明 |
|------|------|
| 禁用 Copilot | 关闭 Copilot 后台常驻 |
| 禁用 Widgets | 关闭任务栏小组件 |
| 关闭必应搜索 | 关闭任务栏搜索中的必应广告 |

### 高危操作（需二次确认）

| 功能 | 说明 | 风险等级 |
|------|------|----------|
| 关闭 UAC | 关闭用户账户控制，需重启生效 | 高 |
| 关闭防火墙 | 域/专用/公用三个配置文件全部关闭 | 高 |
| 压制 Defender | 通过策略压制 Defender 实时防护 | 高 |
| 禁止 Windows 更新 | 禁用 Windows Update 服务和策略 | 高 |

## 技术栈

- **框架**：.NET 9 WPF
- **UI 组件**：WPF-UI 4.3.0（FluentWindow、Mica/Acrylic 自适应）
- **实现方式**：纯原生 C# API，零 PowerShell、零 CMD、零脚本依赖
- **架构**：严格 MVVM（View / ViewModel / Service 三层分离）

## 系统要求

- Windows 10 1903 及以上版本
- Windows 11 全版本（21H2 ~ 25H2）
- **管理员权限**（程序启动时强制提升）

## 编译与发布

### 环境准备

- .NET 9 SDK（x64）
- Visual Studio 2022 或 `dotnet` CLI

### 调试构建

```bash
dotnet build WinTweaker\WinTweaker.csproj -c Debug
```

### 发布模式

#### 模式一：轻量框架依赖版（3~8MB，需目标机已装 .NET 9 运行时）

```bash
dotnet publish WinTweaker\WinTweaker.csproj -c Release -p:PublishProfile=Light
```

输出目录：`WinTweaker\bin\publish-light\`

#### 模式二：自包含完整版（55~80MB，双击即用）

```bash
dotnet publish WinTweaker\WinTweaker.csproj -c Release -p:PublishProfile=Full
```

输出目录：`WinTweaker\bin\publish-full\`

## 项目结构

```
WinTweaker/
├── App.xaml(.cs)              # 程序入口，主题初始化，系统版本校验
├── Assets/                    # 图标资源
├── Models/
│   ├── LogEntry.cs            # 日志条目模型
│   ├── SystemCapabilities.cs  # 系统功能能力掩码
│   └── SystemInfo.cs          # 系统版本信息模型
├── Services/
│   ├── LogService.cs          # 日志系统（四色分级）
│   ├── RegistryService.cs     # 注册表读写封装（64/32位视图）
│   ├── ServiceManager.cs      # Windows 服务管理（缓存原始状态）
│   ├── SystemInfoService.cs   # 系统检测（版本/SKU/Insider）
│   ├── SystemSecurityService.cs # UAC/Defender/防火墙
│   └── UpdateService.cs       # 更新管理（Windows/Edge/Chrome）
├── ViewModels/
│   ├── GeneralViewModel.cs    # 常规优化
│   ├── UpdateViewModel.cs     # 更新管理
│   ├── Win11ViewModel.cs      # Win11 专属
│   ├── DangerViewModel.cs     # 高危操作
│   ├── LogViewModel.cs        # 日志
│   ├── MainViewModel.cs       # 主窗口
│   └── RelayCommand.cs        # MVVM 基础设施
├── Views/
│   ├── MainWindow.xaml(.cs)   # 主窗口（导航框架）
│   ├── GeneralPage.xaml(.cs)  # 常规优化页
│   ├── UpdatePage.xaml(.cs)   # 更新管理页
│   ├── Win11Page.xaml(.cs)    # Win11 专属页
│   ├── DangerPage.xaml(.cs)   # 高危操作页
│   └── LogPage.xaml(.cs)      # 日志页
└── Properties/
    ├── app.manifest           # 管理员权限清单
    └── PublishProfiles/       # 发布配置
```

## 核心设计

### 服务原始状态缓存

所有服务操作前，`ServiceManager` 自动缓存服务原始启动类型。回滚时从内存缓存恢复，不硬编码默认值，确保 100% 还原系统初始状态。

### 系统能力掩码

开机一次性判断所有功能可用性，不支持的功能自动置灰、禁用并提示，不会产生运行时报错。

### 双向可逆

所有优化项均可一键回滚，没有不可逆操作。高危操作页提供"恢复全部"按钮。

### 更新管理技术细节

#### Windows Update 禁用（三层防护）

1. **策略层**：`HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU\NoAutoUpdate = 1`
2. **服务层**：禁用 `wuauserv`、`UsoSvc`、`WaaSMedicSvc` 三个服务
3. **回滚**：删除策略键值 + 还原服务原始启动类型

#### Edge 自动更新禁用

1. **策略注册表**：`HKLM\SOFTWARE\Policies\Microsoft\EdgeUpdate`
   - `UpdateDefault = 0`（禁用所有通道更新）
   - `AutoUpdateCheckPeriodMinutes = 0`（禁止后台检查）
   - `Update{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062} = 0`（Stable 通道）
2. **服务层**：禁用 `edgeupdate`、`edgeupdatem`

#### Chrome 自动更新禁用

1. **策略注册表**（需同时写入 32 位和 64 位视图）：`HKLM\SOFTWARE\Policies\Google\Update`
   - `UpdateDefault = 0`
   - `AutoUpdateCheckPeriodMinutes = 0`
   - `Update{8A69D345-D564-463C-AFF1-A69D9E530F96} = 0`（Chrome Stable GUID）
2. **服务层**：禁用 `gupdate`、`gupdatem`
3. **特殊处理**：Google Update 读取 32 位注册表视图（`KEY_WOW64_32KEY`），故需双视图写入

## 注意事项

- 禁用 Windows Update 后系统将不再接收安全补丁，建议定期手动检查更新
- 禁用浏览器更新后，浏览器 About 页面会显示"更新由组织管理"
- Win11 专属功能在 Win10 系统上自动置灰
- 家庭版系统的组策略相关功能可能受限，程序会自动检测并提示
- 所有修改均可回滚，程序不会进行不可逆操作

## 日志系统

- 带时间戳的四色分级日志：黑(信息)、绿(成功)、黄(警告)、红(错误)
- 支持清空和复制全部日志
- 自动提示版本兼容性警告

## 许可证

MIT License
