# Security Scan Report

**Scan Date:** 2026-01-28 21:05:34 UTC
**Directory:** /home/user/LivestreamerScanner

## Summary

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 4 |
| Medium | 0 |
| Low | 0 |
| **Total** | **4** |

## Rules Checked

- **SEC001**: Hardcoded API Keys
- **SEC002**: Hardcoded Passwords
- **SEC010**: Insecure HTTP URLs
- **SEC011**: SSL/TLS Validation Disabled
- **SEC020**: Potential SQL Injection
- **SEC021**: Potential Command Injection
- **SEC030**: Sensitive Data in Logs
- **SEC040**: Insecure Deserialization
- **SEC050**: Weak Cryptographic Algorithms
- **SEC060**: XSS via innerHTML
- **SEC061**: XSS via document.write
- **SEC071**: CORS Allow All Origins

## Recommendations

1. Store all secrets in environment variables or secure vaults
2. Always use HTTPS for external communications
3. Use parameterized queries for database operations
4. Validate and sanitize all user input
5. Use strong cryptographic algorithms (SHA256, AES)
6. Properly configure CORS with specific origins
