# FireAxe WinUI 3 · 仓库约定

适用于本仓库（`FireAxeWinUI3`）的全部改动。

## 阶段收尾必须写 Phase Notes

每个阶段（P0…P7，见 `docs/PORTING_PLAN.md` §2）结束时，**必须**产出一份
`docs/phase-notes/P<n>-<主题>.md`，并与该阶段的代码进同一个提交。

- 格式、分级标准、索引维护见 `docs/phase-notes/README.md`
- 新阶段从 `docs/phase-notes/_TEMPLATE.md` 复制
- 更新 `docs/phase-notes/README.md` 的索引表
- **没写 notes 的阶段不算完成**

notes 与计划冲突时以 notes 为准，并当天改 `docs/PORTING_PLAN.md`，不许留两份互相矛盾的文档。

## 三条硬约定（同 README）

1. 命名空间保持 `FireAxe` / `FireAxe.Views` / `FireAxe.ViewModels`。
   XAML 取值一律 `{x:Bind r:Texts.Xxx, Mode=OneTime}`（WinUI 3 无 `x:Static`）。
2. 只在新工程修问题；`../FireAxe_Alt` 只读不改，且不做双向同步。
3. 每个阶段结束都要产出一个能双击运行的 exe，不做"全部写完再编译"。

## 已知必须遵守的写法（来自 P0 实测，详见 `docs/phase-notes/P0-spike.md`）

- 所有 ViewModel 继承 `ViewModelBase`，**不得直接继承 `ObservableObject`**
  （`FireAxe.Core` 里有同名类型，会静默让源生成器失效）。
- 可观察属性用 `[ObservableProperty] public partial T X { get; set; }`，
  不用字段写法（MVVMTK0045）；初值放构造函数。
- 派生属性必须显式挂 `[NotifyPropertyChangedFor(nameof(X))]`；
  命令可用态用 `[NotifyCanExecuteChangedFor(nameof(XxxCommand))]`，不手写通知。
- 出现在 `x:DataType` / `ItemTemplate` 里的数据类，属性不能用 `init`。

## 构建

```powershell
dotnet build FireAxe.sln -c Debug
.\FireAxe.WinUI3\bin\x64\Debug\net10.0-windows10.0.26100.0\FireAxe.exe
```
