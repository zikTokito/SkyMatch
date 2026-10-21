# 第三方组件与模型

SkyMatch 以 **AGPL-3.0** 整体发布。以下组件 / 模型各自保留其原始许可证与服务条款。

## 运行时与构建

| 组件 | 版本 / 位置 | 许可证 |
|---|---|---|
| .NET Framework 4.8 | Windows 组件 / 构建目标 | Microsoft 许可 |
| ONNX Runtime | `lib/Microsoft.ML.OnnxRuntime.dll`、`lib/onnxruntime.dll`（1.16.3） | MIT |
| System.Memory / Buffers / Numerics.Vectors / Unsafe | `lib/` | MIT |
| System.Web.Extensions | .NET Framework | Microsoft 许可 |

## 识图模型

### OpenAI CLIP

- 项目：https://github.com/openai/clip
- 许可证：MIT
- SkyMatch 使用 **CLIP ViT-L/14** 的 ONNX 视觉编码器进行零样本天气 / 时段 / 主体分类。
- 模型权重由 OpenAI CLIP 衍生；使用前请同时遵守原始模型和数据集的许可与使用条款。

### Xenova CLIP ONNX 转换

- 模型卡：https://huggingface.co/Xenova/clip-vit-large-patch14
- `models/clip-large/vision_model.onnx` 来自该转换项目的 `clip-vit-large-patch14` ONNX 视觉编码器。
- `models/clip-large/config.json`、`preprocessor_config.json` 为对应配置。
- `models/clip-large/prompt_embeddings.json` 由 SkyMatch 在本地用 CLIP 文本编码器对固定提示词生成，不包含用户数据。

### Ultralytics YOLOv8n

- 项目：https://github.com/ultralytics/ultralytics
- 许可证：**AGPL-3.0**
- `models/yolo/yolov8n.onnx` 用于风景优先时的人物检测。
- 由于 YOLOv8 采用 AGPL-3.0，SkyMatch 整体也以 AGPL-3.0 发布。分发修改后的程序时，请遵守 AGPL 的源码开放与通知义务；如不希望承担该义务，请移除 YOLO 人物检测并替换为许可兼容的检测器。

## 数据与网络服务

| 服务 | 用途 | 条款 |
|---|---|---|
| Open-Meteo | 天气 / 地理编码 | https://open-meteo.com/en/terms |
| freeipapi | IP 定位 | https://freeipapi.com |
| ip-api | IP 定位回退 | https://ip-api.com/docs/legal |
| ipwho.is | IP 定位回退 | https://ipwho.is |
| BigDataCloud | 经纬度反查区县 | https://www.bigdatacloud.com/terms |
| Steam Community / SteamUserContent | 创意工坊页面与预览 | https://store.steampowered.com/legal/ |
| Wallpaper Engine | 壁纸切换 / 音频控制 | https://store.steampowered.com/legal/ |

SkyMatch 与 Valve、Wallpaper Engine、OpenAI、Ultralytics 均无隶属关系。

## 分发说明

Git 仓库不包含模型权重（单文件超过 GitHub 限制），模型通过 **Portable Release** 分发。Release 中的模型仍受上述各自许可证约束；下载、再分发或商用前请自行核对。
