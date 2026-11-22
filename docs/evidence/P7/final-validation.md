<!-- Source: original P7 local verification evidence index. -->
# P7 首轮验证 · 2026-11-11

补充重复回归（2026-11-11）：使用 Release `--no-build --filter FullyQualifiedName~P7RegressionTests` 独立运行五次，每次 8/8 通过、0 跳过，合计 40/40；每次均包含 2000 节点搜索及临时目录推送。原始报告：[1](repeat-1.trx)、[2](repeat-2.trx)、[3](repeat-3.trx)、[4](repeat-4.trx)、[5](repeat-5.trx)。这是有限重复证据，不能证明所有调度下无竞争。

最终运行目录：`artifacts/p7-final/FireAxe-0.1.0-win-x64-20261111-140105-89984733`。
命令：`pwsh -File scripts/Publish-Windows.ps1 -ArtifactRoot artifacts/p7-final`。

| 证据 | 原始结果 |
|---|---|
| [build.txt](build.txt) | Release 0 警告 / 0 错误；21.78 秒 |
| [test.txt](test.txt)、[tests.trx](tests.trx) | 18/18 通过，0 跳过，约 1 秒 |
| [publish.txt](publish.txt) | win-x64 .NET + Windows App SDK self-contained publish 成功 |
| [xaml-resources.txt](xaml-resources.txt) | makepri detailed dump 成功；App、MainWindow、AddonNodeExplorerView 均有 EmbeddedData |

TRX 的 TwoThousandNodes 标准输出（本机样例，非 UI 性能）：

```text
Create 2000 groups: 67 ms
Sort ascending + descending: 6 ms
Search including debounce: 531 ms
Save + reload 2000 groups: 297 ms (data path only; no UI rendering measured)
```

符号链接测试原文：

```text
BCL symlink preflight and AddonRoot.Push succeeded in an isolated temporary game directory.
```

发布流程在 PE 读取阶段退出 1，原始错误：

```text
Test-PublishDependencies.ps1: scripts/Publish-Windows.ps1:161
调用“Read”并传入“1”个参数时发生异常：“Access to the path
'<REPO_ROOT>\artifacts\p7-final\FireAxe-0.1.0-win-x64-20261111-140105-89984733\publish\FireAxe.exe' is denied.”
```

该路径的权限原因未诊断。未修改权限、未完成 ZIP/SHA-256/最终 manifest、未启动发布程序；不要将 publish 成功解释为可分发包通过。当前会话审批关闭，遵照权限诊断技能要求停止此验证路径。较早子代理的 apphost 读取拒绝同样保留于 [distribution-subagent-access-denied.txt](distribution-subagent-access-denied.txt)。

此前两次 2000 节点搜索发生 `Assert.Single() Failure: The collection was empty`，促成订阅顺序与刷新事务串行化修复；最终单次通过尚不能证明长期无竞争。Computer Use 关闭，未采集本轮 UI 截图/键盘/多屏证据。GitHub workflow 未远端执行。真实图片库与干净 Windows 验收未执行。
