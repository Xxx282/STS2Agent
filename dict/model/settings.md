# 游戏设置数据模型

---

## 1. SettingsSave —— 设置存档

**文件**: `src/Core/Saves/SettingsSave.cs`

游戏设置的持久化数据结构，使用 JSON 格式存储。

### 1.1 显示设置

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `FpsLimit` | `int` | 60 | FPS 上限 |
| `WindowPosition` | `Vector2I` | (-1, -1) | 窗口位置（-1 表示居中） |
| `WindowSize` | `Vector2I` | (1920, 1080) | 窗口大小 |
| `Fullscreen` | `bool` | true | 全屏模式 |
| `AspectRatioSetting` | `AspectRatioSetting` | SixteenByNine | 宽高比设置 |
| `TargetDisplay` | `int` | -1 | 目标显示器（-1 表示主显示器） |
| `ResizeWindows` | `bool` | true | 允许调整窗口大小 |
| `VSync` | `VSyncType` | Adaptive | 垂直同步类型 |
| `Msaa` | `int` | 2 | 多重采样抗锯齿级别 |

### 1.2 音频设置

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `VolumeBgm` | `float` | 0.5 | 背景音乐音量（0-1） |
| `VolumeMaster` | `float` | 0.5 | 主音量（0-1） |
| `VolumeSfx` | `float` | 0.5 | 音效音量（0-1） |
| `VolumeAmbience` | `float` | 0.5 | 环境音量（0-1） |

### 1.3 游戏设置

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Language` | `string?` | null | 游戏语言 |
| `SkipIntroLogo` | `bool` | false | 跳过开场 Logo |
| `LimitFpsInBackground` | `bool` | true | 后台运行时限制 FPS |

### 1.4 输入设置

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `KeyboardMapping` | `Dictionary<string, string>` | {} | 键盘按键映射 |
| `ControllerMappingType` | `ControllerMappingType` | - | 手柄映射类型 |
| `ControllerMapping` | `Dictionary<string, string>` | {} | 手柄按键映射 |

### 1.5 Mod 设置

| 字段 | 类型 | 说明 |
|---|---|---|
| `ModSettings` | `ModSettings?` | Mod 配置（ModSettings 类） |

### 1.6 其他设置

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `FullConsole` | `bool` | false | 完整控制台 |
| `SeenEaDisclaimer` | `bool` | false | 是否已看过 EA 免责声明 |

---

## 2. ModSettings —— Mod 配置

**文件**: `src/Core/Modding/ModSettings.cs`

管理 Mod 的启用/禁用状态。

| 字段 | 类型 | 说明 |
|---|---|---|
| `PlayerAgreedToModLoading` | `bool` | 玩家是否同意加载 Mod |
| `ModList` | `List<SettingsSaveMod>` | Mod 列表及其启用状态 |

| 方法 | 说明 |
|---|---|
| `IsModDisabled(Mod mod)` | 检查指定 Mod 是否被禁用 |
| `IsModDisabled(string? id, ModSource source)` | 按 ID 和来源检查 |

---

## 3. 设置界面节点

### 3.1 NSettingsScreen —— 设置界面

**文件**: `src/Core/Nodes/Screens/Settings/NSettingsScreen.cs`

主设置界面，包含多个标签页。

| 信号 | 说明 |
|---|---|
| `SettingsClosed` | 设置界面关闭时触发 |
| `SettingsOpened` | 设置界面打开时触发 |

| 关键字段 | 类型 | 说明 |
|---|---|---|
| `_settingsTabManager` | `NSettingsTabManager` | 标签页管理器 |
| `_feedbackScreenButton` | `NOpenFeedbackScreenButton` | 反馈按钮 |
| `_moddingScreenButton` | `NOpenModdingScreenButton` | Mod 管理按钮 |
| `_toast` | `NSettingsToast` | 提示消息组件 |

| 关键方法 | 说明 |
|---|---|
| `SetIsInRun(bool)` | 设置是否在跑团中（跑团中禁用某些设置） |
| `ShowToast(LocString)` | 显示提示消息 |
| `OpenFeedbackScreen()` | 打开反馈界面（附带截图） |

### 3.2 设置界面标签页

| 标签页 | 节点 | 设置项 |
|---|---|---|
| General（通用） | `NSettingsPanel` (%GeneralSettings) | 语言、快速模式、屏幕震动、提示、计时器、手牌数量、长按确认、跳过 Logo、后 FPS、数据上传、文本效果、反馈、教程重置、Credits、重置设置 |
| Graphics（图形） | `NSettingsPanel` (%GraphicsSettings) | 全屏、显示器选择、窗口分辨率、宽高比、窗口调整、垂直同步、最大 FPS、MSAA、重置图形设置 |
| Sound（声音） | `NSettingsPanel` (%SoundSettings) | 主音量、音乐音量、音效音量、环境音量、后台静音 |

### 3.3 NSettingsTabManager —— 标签页管理器

管理设置界面的多个标签页切换。

### 3.4 NSettingsPanel —— 设置面板

**文件**: `src/Core/Nodes/Screens/Settings/NSettingsPanel.cs`

单个设置面板容器。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Content` | `VBoxContainer` | 设置项容器 |
| `DefaultFocusedControl` | `Control?` | 默认焦点控件 |

---

## 4. 设置控件类型

### 4.1 NSettingsTickbox —— 复选框控件

**文件**: `src/Core/Nodes/Screens/Settings/NSettingsTickbox.cs`

用于开关类型的设置项（如全屏、VSync 等）。

继承自 `NTickbox`，包含焦点选择框效果。

### 4.2 NSettingsSlider —— 滑块控件

**文件**: `src/Core/Nodes/Screens/Settings/NSettingsSlider.cs`

用于数值类型的设置项（如音量、FPS 等）。

| 控件 | 说明 |
|---|---|
| `Slider` | 滑块组件 |
| `SliderValue` | 数值标签 |
| `SelectionReticle` | 焦点选择框 |

| 方法 | 说明 |
|---|---|
| `ConnectSignals()` | 连接信号（替代 `_Ready()`） |
| `OnValueChanged(double)` | 值变更回调 |

### 4.3 NSettingsDropdown —— 下拉框控件

**文件**: `src/Core/Nodes/Screens/Settings/NSettingsDropdown.cs`

用于选择类型的设置项（如分辨率、语言等）。

继承自 `NDropdown`，包含焦点选择框效果。

### 4.4 NSettingsButton —— 按钮控件

用于触发操作的设置项（如重置设置）。

### 4.5 IResettableSettingNode —— 可重置设置接口

**文件**: `src/Core/Nodes/Screens/Settings/IResettableSettingNode.cs`

```csharp
public interface IResettableSettingNode
{
    void ResetToDefault();
}
```

---

## 5. 设置存储管理

### 5.1 SettingsSaveManager —— 设置管理器

**文件**: `src/Core/Saves/Managers/SettingsSaveManager.cs`

| 常量 | 值 | 说明 |
|---|---|---|
| `settingsSaveFileName` | "settings.save" | 存档文件名 |

| 属性 | 类型 | 说明 |
|---|---|---|
| `Settings` | `SettingsSave` | 当前设置 |

| 方法 | 说明 |
|---|---|
| `SaveSettings()` | 保存设置到文件 |
| `LoadSettings()` | 从文件加载设置 |

### 5.2 存档位置

```
%APPDATA%/SlayTheSpire2/
└── settings.save  # JSON 格式设置文件
```

### 5.3 存档格式示例

```json
{
  "schema_version": 5,
  "fps_limit": 60,
  "language": "en",
  "window_position": {"x": -1, "y": -1},
  "window_size": {"x": 1920, "y": 1080},
  "fullscreen": true,
  "aspect_ratio": 0,
  "target_display": -1,
  "resize_windows": true,
  "vsync": 1,
  "msaa": 2,
  "volume_bgm": 0.5,
  "volume_master": 0.5,
  "volume_sfx": 0.5,
  "volume_ambience": 0.5,
  "skip_intro_logo": false,
  "keyboard_mapping": {},
  "controller_mapping_type": 0,
  "controller_mapping": {},
  "limit_fps_in_background": true,
  "full_console": false,
  "seen_ea_disclaimer": true,
  "mod_settings": {
    "mods_enabled": true,
    "mod_list": []
  }
}
```

---

## 6. 设置界面节点树结构

```
NSettingsScreen
├── %SettingsTabManager (NSettingsTabManager)
│   └── [Tab 按钮...]
├── %GeneralSettings (NSettingsPanel)
│   └── VBoxContainer
│       ├── LanguageLine / LanguageDropdown
│       ├── FastMode (NSettingsTickbox)
│       ├── Screenshake (NSettingsTickbox)
│       ├── CommonTooltips (NSettingsTickbox)
│       └── ...
├── %GraphicsSettings (NSettingsPanel)
│   └── VBoxContainer
│       ├── Fullscreen (NSettingsTickbox)
│       ├── DisplaySelection (NSettingsDropdown)
│       ├── WindowedResolution (NSettingsDropdown)
│       └── ...
├── %SoundSettings (NSettingsPanel)
│   └── VBoxContainer
│       ├── MasterVolume (NSettingsSlider)
│       ├── BgmVolume (NSettingsSlider)
│       └── ...
├── %FeedbackButton (NOpenFeedbackScreenButton)
├── %ModdingButton (NOpenModdingScreenButton)
└── %Toast (NSettingsToast)
```

---

## 7. Mod 如何添加自定义设置

### 7.1 Mod 设置存储

Mod 设置通过 `SettingsSave.ModSettings` 存储：

```csharp
public class SettingsSave
{
    [JsonPropertyName("mod_settings")]
    public ModSettings? ModSettings { get; set; }
}

public class ModSettings
{
    [JsonPropertyName("mods_enabled")]
    public bool PlayerAgreedToModLoading { get; set; }
    
    [JsonPropertyName("mod_list")]
    public List<SettingsSaveMod> ModList { get; set; }
}

public class SettingsSaveMod
{
    public string Id { get; set; }
    public ModSource Source { get; set; }
    public bool IsEnabled { get; set; }
}
```

### 7.2 检查 Mod 是否启用

```csharp
// 检查当前 Mod 是否被禁用
if (SaveManager.Instance.SettingsSave.ModSettings?.IsModDisabled(this) ?? false)
{
    // Mod 被禁用
}

// 检查其他 Mod 是否启用
var modSettings = SaveManager.Instance.SettingsSave.ModSettings;
if (modSettings?.IsModDisabled("other_mod_id", ModSource.Workshop) ?? false)
{
    // 其他 Mod 被禁用
}
```

### 7.3 设置界面行为

- **跑团中禁用**：某些设置在跑团中无法修改（见 `NSettingsScreen._Ready()`）
  - 语言设置（灰色显示）
  - Mod 管理按钮（禁用）

- **Steam Deck 默认值**：检测到 Steam Deck 时自动应用特定默认值
  ```csharp
  if (SteamInitializer.Initialized && SteamUtils.IsSteamRunningOnSteamDeck())
  {
      settings.Fullscreen = true;
      settings.WindowPosition = new Vector2I(0, 0);
  }
  ```
