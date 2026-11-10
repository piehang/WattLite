# WattLite

从 [Watt Toolkit (Steam++)](https://github.com/BeyondDimension/SteamTools) 中剥离出来的轻量游戏加速器。

原版功能很多，但启动慢。这里只保留「游戏加速」这一件事：常驻系统托盘，启动后自动开启加速，退出时自动还原 hosts。

## 功能

- 托盘常驻，启动即自动开启加速（无需手动点）
- 加速域名：Steam / Twitch / Origin / Uplay / Roblox / Nexus Mods / Mod.io / GitHub
- 右键菜单：启动加速 / 停止加速 / 查看日志 / 打开程序目录 / 退出
- 退出时自动停止反向代理、结束加速器子进程并还原 hosts，不留残留
- 提供 Windows 安装包（含开始菜单、桌面快捷方式与卸载功能）

## 工作原理

1. 以 IPC 服务端身份启动官方 `Steam++.Accelerator.exe` 子进程（`dotnetCampus.Ipc`）
2. 通过 `IReverseProxyService.StartProxyAsync` 下发精简后的加速项目（`domains.json`）
3. 子进程监听 80/443 做反向代理，本程序把对应域名写入 hosts 指向 127.0.0.1
4. 停止/退出时调用 `StopProxyAsync` 并删除 hosts 中的标记区块

为保证子进程的 `AssemblyInfo.ValidateAssembly` 版本校验通过，程序集的 Title / Product / Company / Copyright / Description 必须与官方逐字一致。

## 目录

| 路径 | 说明 |
| --- | --- |
| `*.cs` | 主程序源码 |
| `domains.json` | 内置加速规则（由官方 `LOCAL_ACCELERATE.json` 解码后精简） |
| `libs/` | 编译/运行所需的官方 DLL（白名单裁剪） |
| `Accelerator/` | 官方加速器子进程（反向代理核心） |
| `dotnet/` | 便携版 .NET 10 运行时（NETCore.App + AspNetCore.App） |
| `installer/WattLite.nsi` | NSIS 安装包脚本 |

## 构建前提

仓库已自包含：克隆后只需安装 .NET 10 SDK，直接构建即可，无需再安装 Watt Toolkit 或复制任何官方文件。

## 构建

```powershell
dotnet publish WattLite.csproj -c Release -r win-x64 -o publish
makensis installer\WattLite.nsi
```

## 说明

本项目为个人自用工具。`Accelerator/` 与 `libs/` 内的二进制文件来自 Watt Toolkit（Steam++，GPL-3.0），版权归原作者所有；`dotnet/` 为微软官方 .NET 运行时。如原作者认为不妥请联系下架。
