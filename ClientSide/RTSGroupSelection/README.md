# RTS Style Group Selection Mod

一个模仿 RTS 游戏单位编组选择逻辑的
Mod，用于改进选择与控制单位组的操作体验。

## 依赖

本 Mod 使用 **额外输入框架 Mod（Extra Input Framework）** 来提供：

-   更方便的按键绑定
-   更稳定 / 可预测的按键输入检测

按键绑定可以直接通过**游戏原生的按键绑定界面**进行设置。

> ⚠️ 由于代码机制限制，本 Mod **无法提供默认按键绑定**。\
> 安装后请手动在游戏的按键设置中进行绑定。

## 按键分类

本 Mod 新增的所有按键在绑定界面中具有以下特点：

-   分类：**Debug**
-   前缀：`GroupSelection::`

该前缀用于标识这些按键由本 Mod 提供和使用。

## 使用方法

操作逻辑类似于 RTS 游戏中的控制组：

-  直接按 **组号键**        用保存的组替换当前选择

-  **SaveKey + 组号键**     将当前选择保存到对应组

-  **AppendKey + 组号键**   将该组追加到当前选择

## 已知问题

⚠ **Shift + 小键盘数字键** 会产生符号输入（特殊字符），\
这会导致输入检测失败。

为了避免问题：

-   ❌ 不要使用 `Shift + 小键盘数字`
-   ✔ 可以使用 `Shift + 上方数字键`
-   ✔ 可以使用 `其他控制键 + 小键盘数字`

------------------------------------------------------------------------

# English Version

# RTS Style Group Selection Mod

A mod that implements **RTS-style control group selection logic**,
improving the workflow for selecting and managing groups.

## Dependency

This mod relies on the **Extra Input Framework mod**, which provides:

-   More convenient key binding
-   More stable and predictable input detection

All bindings can be configured using the **game's built-in key binding
interface**.

> ⚠️ Due to technical limitations, this mod **cannot define default key
> bindings**.\
> You must assign the keys manually in the game's control settings.

## Key Categories

All keys introduced by this mod have the following characteristics in
the binding interface:

-   Category: **Debug**
-   Prefix: `GroupSelection::`

This prefix identifies the keys as being added and used by this mod.

## Usage

The control logic follows common RTS control group behavior:

-  Press **Group Number Key**          Replace current selection with the
                                      saved group

-  **SaveKey + Group Number Key**      Save the current selection to the
                                      group

-  **AppendKey + Group Number Key**    Add the selected group to the
                                      current selection
  -----------------------------------------------------------------------

## Known Issue

⚠ **Shift + Numpad Number Keys** generate symbol inputs (special
characters), which causes input detection to fail.

To avoid this issue:

-   ❌ Do **not** use `Shift + Numpad Numbers`
-   ✔ `Shift + Top Row Numbers` works normally
-   ✔ `Other modifier keys + Numpad Numbers` also work normally
