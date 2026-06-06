# CountermeasureIndex

允许同时释放多种干扰措施，通过将原版单选索引语义修改为位掩码语义实现。

Allows simultaneous deployment of multiple countermeasures by reinterpreting the vanilla single-selection index as a bitmask.

---

## 前置依赖 / Dependencies

本 mod 需要使用 **InputFramework**（额外输入框架），请确保你已正确安装该框架。

This mod requires **InputFramework**. Please make sure it is correctly installed.

## 按键绑定 / Key Bindings

请在游戏本体 **绑定按键** 界面下的 **Flight** 栏目中绑定额外的按键：

Please bind the following extra keys under the **Flight** category in the game's **Bind Keys** menu:

| 动作 / Action | 说明 / Description |
|---|---|
| `CountermeasureIndex::DeployFlares` | 释放热诱弹 / Deploy Flares |
| `CountermeasureIndex::DeployECM` | 释放 ECM / Deploy ECM |

---

## ⚠️ 警告 / WARNING

> **!!!!!!!!!!!! 警告 !!!!!!!!!!!!**

本 mod 有 **兼容性模式（`CompatibilityMode`）** 配置项，可通过 **ConfigurationManager（F1 菜单）** 或配置文件修改。

**在对局中，所有玩家都必须采取完全相同的配置。**

> 如果有人没有安装本 mod，则视为该玩家使用 **"兼容性模式：开"** 的配置。
>
> 如果配置不一致，将会引发严重问题，导致被踢掉线。

---

> **!!!!!!!!!!!! WARNING !!!!!!!!!!!!**

This mod has a **Compatibility Mode (`CompatibilityMode`)** setting, configurable via **ConfigurationManager (F1 menu)** or the config file.

**In a session, ALL players MUST use the exact same configuration.**

> If a player does NOT have this mod installed, they are treated as using **Compatibility Mode: ON**.
>
> Mismatched configurations will cause severe issues and result in disconnection.

---

## 原理 / How It Works

### 兼容性模式：关 / Compatibility Mode: OFF

在此模式下，你可以**同时释放多个干扰措施**。这是通过对原游戏的网络通讯语义进行修改实现的。

In this mode, you can **deploy multiple countermeasures simultaneously**. This is achieved by modifying the network communication semantics of the vanilla game.

原游戏使用如下数据结构来表达干扰措施操作：

The vanilla game uses the following data structure to express countermeasure actions:

```json
{
    "trigger": "bool",
    "index": "byte"
}
```

语义：释放/不释放 **某一号** 干扰措施。该语句只能表达**一种**干扰措施的释放与否。

Semantics: Deploy / do not deploy countermeasure **at index X**. This message can only express the deployment of **a single** countermeasure type.

---

本 mod 在兼容性模式：关时，将语义修改为：

When Compatibility Mode is OFF, this mod changes the semantics to:

```json
{
    "trigger": "bool",
    "index(mask)": "byte"
}
```

通过将 `index` 重新解释为**位掩码（bitmask）**，最多可表达 **八个通道** 的干扰措施启用与否。

By reinterpreting `index` as a **bitmask**, up to **eight channels** of countermeasures can be toggled simultaneously.

当按下绑定的按键时，`index` 中对应的位将被置为 `1`，以表达选中该干扰措施。

When a bound key is pressed, the corresponding bit in `index` is set to `1`, indicating that countermeasure is selected.

---

> 由于本质上是**语义修改**，因此通信双方必须使用相同的 mod 和兼容性配置，否则会引发：
>
> 1. **越界异常** — 服务端错误地将掩码当作序号。掩码转换为数字通常非常大，会导致数组越界。
> 2. **释放错误** — 服务端错误地将序号当作掩码，导致释放错误的干扰措施。

> Since this is fundamentally a **semantic change**, both sides of the communication must use the same mod and compatibility configuration. Otherwise, the following will occur:
>
> 1. **Out-of-bounds exception** — The server mistakenly treats the bitmask as an index. Bitmask values are typically very large numbers, causing array out-of-bounds errors.
> 2. **Incorrect deployment** — The server mistakenly treats the index as a bitmask, causing the wrong countermeasures to be deployed.

---

### 兼容性模式：开 / Compatibility Mode: ON

在此模式下，mod **不修改通讯语义**。按下绑定的按键时，将**快速切换到对应的干扰措施并释放**。

In this mode, the mod does **NOT modify communication semantics**. When a bound key is pressed, it will **quickly switch to the corresponding countermeasure and deploy it**.
