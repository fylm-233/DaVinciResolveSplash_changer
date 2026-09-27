# Resolve Splash Studio 验证报告

验证日期：2026-09-27

## 测试对象

- DaVinci Resolve 20.3.2.9
- `Resolve.exe` 大小：`640,545,824` 字节
- 原始 Authenticode：`Valid`
- 签名者：`Blackmagic Design Pty Ltd.`

## 已通过的验证

### 1. PE 解析和原始 Checksum

- Windows `imagehlp.CheckSumMappedFile` 计算结果：`0x262E50B3`
- EXE 头部保存值：`0x262E50B3`
- 结果：一致

### 2. PNG 扫描

- 扫描 640 MB 真实 Resolve.exe
- 找到 500px 以上有效 PNG：112 个
- 默认“疑似启动图”筛选：54 个
- 找到 `2220 x 980` 启动图资源
- 扫描耗时约 3.5 秒

### 3. PNG 无损重建

- 提取真实 2220x980 PNG
- 逐 chunk CRC 验证通过
- 重建为原始精确长度通过
- 重压缩后的 zlib 流再次完整解压通过
- 解压得到的原始扫描线数据：8,703,380 字节

### 4. 640 MB 实际补丁副本

- 输入和输出文件长度均为 `640,545,824` 字节
- 输出资源 SHA-256 与替换数据一致
- 输出 PNG 可重新解析，尺寸仍为 2220x980
- PE Checksum 从 `0x262E50B3` 更新为 `0x262E1239`
- 使用 Windows API 独立复算，输出 Checksum 一致
- 生成了有效 JSON 清单
- `length_preserved = true`
- `pe_checksum_verified = true`

### 5. Authenticode 两种策略

保留原证书区：

- `Get-AuthenticodeSignature`：`HashMismatch`
- 这是修改内容后无法避免的结果，因为缺少 Blackmagic 私钥

清除安全目录：

- `Get-AuthenticodeSignature`：`NotSigned`
- PE Checksum 复核仍通过

### 6. GUI 烟测

- 成功启动 WinForms 窗口
- 自动扫描真实 Resolve.exe
- 显示 54 个疑似启动图资源
- 选中第一个资源后成功加载 `2220x980` 预览
- UI 截图检查通过，左栏资源列表和双预览区域布局正常

## 结论

工具已满足以下核心目标：

- 有可用 GUI
- 能扫描和预览 Resolve 内嵌 PNG
- 能精确保持原文件长度
- 能正确处理 PNG chunk CRC
- 能重新计算和验证 PE Checksum
- 能生成补丁副本、备份和恢复
- 对无法伪造的厂商 Authenticode 签名给出了明确且正确的处理方式

## 批量替换补充验证（2026-09-27）

- 列表已启用 `MultiSelect`，支持 Shift/Ctrl 多选。
- 自动化鼠标框选测试：一次框选 3 行，通过。
- “全选同尺寸”测试：一次选中 19 张 `2220x980` 启动图。
- 批量构建测试：生成 19 个不同原始长度的精确替换项。
- 真实 640 MB 批补丁测试：一次写入 19 个资源。
- 批补丁输出长度：`640,545,824` 字节，保持不变。
- 批补丁 PE Checksum 复核通过：`0x262EC14E`。
