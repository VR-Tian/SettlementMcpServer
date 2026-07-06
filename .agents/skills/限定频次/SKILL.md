---
name: Frequency Limit Analysis Rules（限定频次分析规则）
description: >-
  Used to analyze frequency limit rules for medical insurance settlement data、
  Used when users need to analyze frequency limit rules for medical insurance settlement data、
  当用户需要对医保结算数据进行限定频次规则分析时使用Skill。
metadata:
  author: Tian
  version: "1.0"
---
此规则内涵必须必须配套指定MCP使用，否则无法正常工作。

## 限定频次规则内涵
```Stdio
通过Stdio协议调用医保结算数据审核数据MCP接口[exec_audit_analysis]传入限定频次规则编码[120501]
