# 仓库升级 / Warehouse Upgrades

作者：yafaya

为 Stacklands 增加三级仓库，减少扩容所需的建筑卡数量。

| 建筑 | 合成材料 | 卡牌上限加成 | 制作时间 |
| --- | --- | --- | --- |
| 大仓库 | 2 个普通仓库 | +30 | 10 秒 |
| 更大仓库 | 3 个大仓库 | +100 | 20 秒 |
| 超大仓库 | 5 个更大仓库 | +800 | 30 秒 |

## 游戏内配置

打开 **Mod Options → 仓库升级 / Warehouse Upgrades**，可分别修改三个等级的「所需仓库数量」和「容量加成」。上表是默认值。

- 合成数量范围：1～100。
- 容量加成范围：1～1,000,000。
- 返回时自动保存配置，游戏重新加载后生效，已有和新建仓库均使用新容量。
- 配方消耗和卡牌容量说明同步更新。设为1时，单张材料就会自动开始升级。

配置保存在安装目录的 `config.json`，作用于该 Mod 下的所有存档。

## 安装

1. 保存并退出游戏。
2. 解压 `dist/sl_warehouse.zip`，将其中的 `sl_warehouse` 文件夹放入 `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods`。
3. 启动游戏，在 Mods 菜单中确认“仓库升级 / Warehouse Upgrades”已启用。

正确的目录结构为 `Mods/sl_warehouse/manifest.json`，同目录包含 `sl_warehouse.dll`、`Cards`、`Blueprints`、`Icons` 和 `localization.tsv`。

使用游戏自带的 Mod 加载器和 Harmony，不需要额外安装前置框架。运行时验证版本为 Steam Build ID `16550853`。

## 使用

将配方指定数量的同级仓库叠在一起即可制作，不需要村民。精确匹配材料数量；额外卡牌会使配方不匹配。配方无需先抽到创意卡即可使用，创意卡也加入了高级建筑创意卡池。

三个等级均使用独立图标。容量加成可累计，随建筑在棋盘间移动而转移，建筑出售或移除后取消。城市 DLC 继续使用市政厅规则。

超大仓库必定为闪卡，包括新合成的建筑和旧存档中已有的超大仓库。沿用原版闪卡五倍售价，售价为750；重复读档不会继续翻倍。

## 构建与检查

Windows 自带的 .NET Framework C# 编译器直接引用本地游戏程序集，不需要安装 .NET SDK。

```powershell
$stacklandsPath = Read-Host '请输入 Stacklands 游戏安装目录'
pwsh -NoProfile -File .\build.ps1 -GamePath $stacklandsPath
python .\tests\check_content.py
```

输出为 `dist/sl_warehouse/` 和 `dist/sl_warehouse.zip`。ZIP 不包含游戏程序集或测试 Mod。

运行游戏内自动检查前，需要保存并退出游戏：

```powershell
pwsh -NoProfile -File .\tests\run-runtime.ps1 -GamePath $stacklandsPath
```

该检查暂存当前安装，临时安装测试版本，以两个独立游戏进程验证默认行为及配置保存、再次启动后的生效情况，完成后还原原安装和配置。结果位于 `dist/runtime-defaults-results.txt` 和 `dist/runtime-configured-results.txt`。

## 更新与卸载

更新时先退出游戏，再将新版本文件覆盖到 `Mods/sl_warehouse`，保留已有的 `config.json`。

卸载前，在每个含升级仓库的存档中卖掉或移除这三种自定义建筑，再保存退出；否则存档会引用缺失卡牌。移除仓库后应留意新的卡牌上限，再继续推进月份。随后删除 `Mods/sl_warehouse`。
