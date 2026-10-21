# Models

模型权重不纳入 Git 仓库。

请从最新 **Portable** Release 导入，或手动放置以下文件：

```text
models/
├── clip-large/
│   ├── vision_model.onnx
│   ├── config.json
│   ├── preprocessor_config.json
│   └── prompt_embeddings.json
└── yolo/
    └── yolov8n.onnx
```

推荐：

```powershell
.\scripts\import-models.ps1 -PortableZip .\SkyMatch-1.2.0-Portable.zip
```

没有模型时程序仍可启动，但会退回预览特征 + 关键词兜底，识别质量下降。

模型来源与许可证见 [`../THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md)。
