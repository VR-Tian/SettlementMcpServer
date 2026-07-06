---
name: Duplicate Charge Analysis Rules（重复收费分析规则）
description: >-
  Used to analyze duplicate charge rules for medical insurance settlement data、
  Used when users need to analyze duplicate charge rules for medical insurance settlement data、
  当用户需要对医保结算数据进行重复收费规则分析时使用Skill。
metadata:
  author: Tian
  version: "1.0"
---
此规则内涵必须必须配套指定MCP使用，否则无法正常工作。

## 重复收费规则内涵
```Stdio
通过Stdio协议调用医保结算数据审核数据MCP接口[exec_audit_analysis]传入重复收费规则编码[130301]