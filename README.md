# FbSample

## 技术栈

| 类别 | 说明                                                                                        |
| --- |---------------------------------------------------------------------------------------------|
| 开发语言 | C# 12                                                                                       |
| 目标框架 | .NET 8，`net8.0-windows`                                                                    |
| .NET SDK | `8.0.425`，通过 `global.json` 固定                                                          |
| 桌面 UI | Windows Forms（WinForms）、AntdUI `2.4.8`                                                   |
| 多语言 | `.resx` 资源与 AntdUI 本地化，支持英语（`en-US`）、简体中文（`zh-CN`）、繁体中文（`zh-TW`） |
| DPI 缩放 | PerMonitorV2                                                                                |
| 依赖管理 | NuGet 中央包版本管理与 `packages.lock.json` 锁文件                                          |
| 自动化测试 | MSTest `4.3.3`、Microsoft.NET.Test.Sdk `18.9.0`，STA 桌面交互与渲染集成测试                 |
| 代码检查 | .NET 内置分析器、可空引用类型、编译警告视为错误、`.editorconfig`                            |

## 目录结构

```text
FbSample/
├── .idea/                                           # Rider 项目配置
│   └── .idea.FbSample/
│       └── .idea/
│           ├── .gitignore
│           ├── encodings.xml
│           ├── indexLayout.xml
│           └── vcs.xml
├── src/                                             # 应用源码
│   └── FbSample/
│       ├── Localization/                            # 本地化提供器与三语言文本资源
│       │   ├── AppLocalization.cs
│       │   ├── Strings.resx
│       │   ├── Strings.zh-CN.resx
│       │   └── Strings.zh-TW.resx
│       ├── Properties/                              # 强类型项目资源
│       │   ├── Resources.Designer.cs
│       │   └── Resources.resx
│       ├── Resources/                               # 应用图标与透明导航 Logo
│       │   ├── App.ico
│       │   └── NavigationLogo.png
│       ├── Views/                                   # 窗体、页面与控件
│       │   ├── Controls/                            # 导航菜单、子菜单浮窗与设置卡片
│       │   │   ├── NavigationFlyoutControl.cs
│       │   │   ├── NavigationFlyoutControl.Designer.cs
│       │   │   ├── NavigationFlyoutControl.resx
│       │   │   ├── NavigationFlyoutWindow.cs
│       │   │   ├── NavigationMenu.cs
│       │   │   └── SettingsCardPanel.cs
│       │   ├── Pages/                               # 页面控件、设计器与资源
│       │   │   ├── DashboardPage.cs
│       │   │   ├── DashboardPage.Designer.cs
│       │   │   ├── DashboardPage.resx
│       │   │   ├── DebugPage1Page.cs
│       │   │   ├── DebugPage1Page.Designer.cs
│       │   │   ├── DebugPage1Page.resx
│       │   │   ├── DevicesPage.cs
│       │   │   ├── DevicesPage.Designer.cs
│       │   │   ├── DevicesPage.resx
│       │   │   ├── LogsPage1Page.cs
│       │   │   ├── LogsPage1Page.Designer.cs
│       │   │   ├── LogsPage1Page.resx
│       │   │   ├── LogsPage2Page.cs
│       │   │   ├── LogsPage2Page.Designer.cs
│       │   │   ├── LogsPage2Page.resx
│       │   │   ├── MonitorPage.cs
│       │   │   ├── MonitorPage.Designer.cs
│       │   │   ├── MonitorPage.resx
│       │   │   ├── SettingsPage.cs
│       │   │   ├── SettingsPage.Designer.cs
│       │   │   └── SettingsPage.resx
│       │   ├── AboutControl.cs                      # 关于弹窗内容
│       │   ├── AboutControl.Designer.cs
│       │   ├── AboutControl.resx
│       │   ├── MainForm.cs                          # 主窗体、标题栏、导航栏与页面容器
│       │   ├── MainForm.Designer.cs
│       │   └── MainForm.resx
│       ├── FbSample.csproj                          # WinForms 应用项目
│       ├── packages.lock.json                       # 应用依赖锁文件
│       └── Program.cs                               # 应用入口
├── tests/                                           # 测试源码
│   └── FbSample.Tests/
│       ├── DesktopUiTests.cs                        # STA 桌面交互与渲染集成测试
│       ├── FbSample.Tests.csproj                    # 测试项目
│       └── packages.lock.json                       # 测试依赖锁文件
├── .editorconfig                                    # 代码格式与命名规则
├── .gitignore                                       # Git 忽略规则
├── Directory.Build.props                            # 统一编译与分析规则
├── Directory.Packages.props                         # NuGet 包版本
├── FbSample.sln                                     # 解决方案
├── global.json                                      # .NET SDK 版本
├── LICENSE                                          # Apache-2.0 项目授权与第三方声明
└── README.md                                        # README 文档
```
