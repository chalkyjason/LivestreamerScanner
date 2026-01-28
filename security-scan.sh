#!/bin/bash

# Security Scanner Agent - Shell Version
# Scans codebase for common security vulnerabilities

set -e

SCAN_DIR="${1:-.}"
REPORT_FILE="security-report.md"

# Exclude patterns for files that contain security rule definitions (to avoid false positives)
EXCLUDE_PATTERN="SecurityScannerService|security-scan\.sh"

echo ""
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║         SECURITY SCANNER AGENT v1.0 (Shell)                    ║"
echo "║         Automated Security Vulnerability Detection             ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
echo "Scanning directory: $SCAN_DIR"
echo ""

CRITICAL_COUNT=0
HIGH_COUNT=0
MEDIUM_COUNT=0
LOW_COUNT=0
FINDINGS=""

add_finding() {
    local severity="$1"
    local rule_id="$2"
    local title="$3"
    local file="$4"
    local line_num="$5"
    local description="$6"
    local recommendation="$7"
    local line_content="$8"

    case "$severity" in
        CRITICAL) ((CRITICAL_COUNT++)) || true; icon="🔴" ;;
        HIGH) ((HIGH_COUNT++)) || true; icon="🟠" ;;
        MEDIUM) ((MEDIUM_COUNT++)) || true; icon="🟡" ;;
        LOW) ((LOW_COUNT++)) || true; icon="🟢" ;;
    esac

    FINDINGS="$FINDINGS
  $icon [$rule_id] $title
     File: $file:$line_num
     $description"
}

echo "Checking for hardcoded secrets..."

# SEC001: Hardcoded API Keys
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    # Skip scanner's own files and comments/placeholders
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    if [[ ! "$content" =~ ^[[:space:]]*(//|\*|#) ]] && [[ ! "$content" =~ YOUR_ ]] && [[ ! "$content" =~ EXAMPLE_ ]]; then
        add_finding "HIGH" "SEC001" "Potential Hardcoded API Key" "$file" "$line_num" "Detected potential hardcoded API key pattern" "Store API keys in environment variables" "$content"
    fi
done < <(grep -rn -E "api[_-]?key[\"']\s*[=:]\s*[\"'][^\"']{15,}[\"']" "$SCAN_DIR" --include="*.cs" --include="*.js" --include="*.ts" --include="*.json" 2>/dev/null || true)

# SEC002: Hardcoded Passwords
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    if [[ ! "$content" =~ ^[[:space:]]*(//|\*|#) ]] && [[ ! "$content" =~ YOUR_ ]] && [[ ! "$content" =~ PLACEHOLDER ]]; then
        add_finding "CRITICAL" "SEC002" "Potential Hardcoded Password" "$file" "$line_num" "Detected potential hardcoded password or secret" "Never hardcode passwords" "$content"
    fi
done < <(grep -rn -E "(password|passwd|secret|token)[\"']?\s*[=:]\s*[\"'][^\"']{4,}[\"']" "$SCAN_DIR" --include="*.cs" --include="*.js" --include="*.json" 2>/dev/null | grep -vi "placeholder\|example\|your_\|xxx" || true)

echo "Checking for insecure communication..."

# SEC010: Insecure HTTP URLs
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    if [[ ! "$content" =~ ^[[:space:]]*(//|\*|#) ]] && [[ ! "$content" =~ localhost ]] && [[ ! "$content" =~ 127\.0\.0\.1 ]]; then
        add_finding "MEDIUM" "SEC010" "Insecure HTTP URL" "$file" "$line_num" "Using HTTP instead of HTTPS" "Use HTTPS for secure communication" "$content"
    fi
done < <(grep -rn -E "http://[^\"'\s]+" "$SCAN_DIR" --include="*.cs" --include="*.js" --include="*.ts" --include="*.html" 2>/dev/null | grep -v "localhost\|127.0.0.1\|schemas\|xml\|w3.org" || true)

# SEC011: SSL Validation Disabled
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "CRITICAL" "SEC011" "SSL/TLS Validation Disabled" "$file" "$line_num" "Disabling SSL certificate validation is dangerous" "Enable proper certificate validation" "$content"
done < <(grep -rn -E "ServerCertificateValidationCallback\s*=|CheckCertificateRevocationList\s*=\s*false" "$SCAN_DIR" --include="*.cs" 2>/dev/null || true)

echo "Checking for injection vulnerabilities..."

# SEC020: SQL Injection
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "CRITICAL" "SEC020" "Potential SQL Injection" "$file" "$line_num" "String concatenation in SQL can lead to SQL injection" "Use parameterized queries" "$content"
done < <(grep -rn -E "(ExecuteSql|SqlCommand|ExecuteNonQuery).*\+" "$SCAN_DIR" --include="*.cs" 2>/dev/null || true)

# SEC021: Command Injection
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "CRITICAL" "SEC021" "Potential Command Injection" "$file" "$line_num" "User input in system commands is dangerous" "Validate and sanitize input" "$content"
done < <(grep -rn -E "Process\.Start.*\+" "$SCAN_DIR" --include="*.cs" 2>/dev/null || true)

echo "Checking for XSS vulnerabilities..."

# SEC060: innerHTML usage (note: can be safe if content is escaped)
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    if [[ ! "$content" =~ ^[[:space:]]*(//|\*) ]]; then
        add_finding "HIGH" "SEC060" "Potential XSS - innerHTML" "$file" "$line_num" "innerHTML can lead to XSS attacks - verify content is escaped" "Use textContent or ensure proper escaping" "$content"
    fi
done < <(grep -rn -E "\.innerHTML\s*=" "$SCAN_DIR" --include="*.js" --include="*.ts" --include="*.html" 2>/dev/null || true)

# SEC061: document.write
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "HIGH" "SEC061" "Potential XSS - document.write" "$file" "$line_num" "document.write() can introduce XSS" "Use DOM manipulation methods" "$content"
done < <(grep -rn -E "document\.write\s*\(" "$SCAN_DIR" --include="*.js" --include="*.ts" --include="*.html" 2>/dev/null || true)

echo "Checking for weak cryptography..."

# SEC050: Weak crypto algorithms
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "HIGH" "SEC050" "Weak Cryptographic Algorithm" "$file" "$line_num" "MD5/SHA1/DES are cryptographically weak" "Use SHA256 or AES" "$content"
done < <(grep -rn -E "(MD5|SHA1|DES)CryptoServiceProvider|MD5\.Create|SHA1\.Create" "$SCAN_DIR" --include="*.cs" 2>/dev/null || true)

echo "Checking for insecure deserialization..."

# SEC040: Insecure Deserialization
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "HIGH" "SEC040" "Insecure Deserialization" "$file" "$line_num" "BinaryFormatter is vulnerable to deserialization attacks" "Use System.Text.Json" "$content"
done < <(grep -rn -E "BinaryFormatter|SoapFormatter|NetDataContractSerializer" "$SCAN_DIR" --include="*.cs" 2>/dev/null || true)

echo "Checking for sensitive data exposure..."

# SEC030: Sensitive Data in Logs - stricter pattern to reduce false positives
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    # Only flag if it looks like actual logging of sensitive values
    if [[ ! "$content" =~ ^[[:space:]]*(//|\*|#) ]] && [[ "$content" =~ \{.*password|\{.*token|\{.*secret ]]; then
        add_finding "MEDIUM" "SEC030" "Sensitive Data in Logs" "$file" "$line_num" "Logging sensitive data can expose secrets" "Mask sensitive information" "$content"
    fi
done < <(grep -rn -E "(Log\.|Console\.Write|Debug\.Write).*\{.*password|token|secret" "$SCAN_DIR" --include="*.cs" --include="*.js" 2>/dev/null | grep -vi "placeholder\|example" || true)

echo "Checking for misconfigurations..."

# SEC071: CORS Allow All
while IFS=: read -r file line_num content; do
    [ -z "$file" ] && continue
    [[ "$file" =~ $EXCLUDE_PATTERN ]] && continue
    add_finding "MEDIUM" "SEC071" "CORS Allow All Origins" "$file" "$line_num" "Allowing all CORS origins is risky" "Restrict to trusted origins" "$content"
done < <(grep -rn -E "AllowAnyOrigin|Access-Control-Allow-Origin.*\*" "$SCAN_DIR" --include="*.cs" --include="*.js" 2>/dev/null || true)

TOTAL_COUNT=$((CRITICAL_COUNT + HIGH_COUNT + MEDIUM_COUNT + LOW_COUNT))

echo ""
echo "┌─────────────────────────────────────────────────────────────────┐"
echo "│  SUMMARY                                                        │"
echo "├─────────────────────────────────────────────────────────────────┤"
printf "│  Critical: %-5s High: %-5s Medium: %-5s Low: %-5s       │\n" "$CRITICAL_COUNT" "$HIGH_COUNT" "$MEDIUM_COUNT" "$LOW_COUNT"
printf "│  Total Findings: %-47s │\n" "$TOTAL_COUNT"
echo "└─────────────────────────────────────────────────────────────────┘"
echo ""

if [ "$TOTAL_COUNT" -eq 0 ]; then
    echo "  ✓ No security issues found!"
else
    echo "  FINDINGS:"
    echo "$FINDINGS"
fi

# Generate Markdown Report
cat > "$SCAN_DIR/$REPORT_FILE" << EOF
# Security Scan Report

**Scan Date:** $(date -u '+%Y-%m-%d %H:%M:%S') UTC
**Directory:** $SCAN_DIR

## Summary

| Severity | Count |
|----------|-------|
| Critical | $CRITICAL_COUNT |
| High | $HIGH_COUNT |
| Medium | $MEDIUM_COUNT |
| Low | $LOW_COUNT |
| **Total** | **$TOTAL_COUNT** |

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
EOF

echo ""
echo "Detailed report saved to: $SCAN_DIR/$REPORT_FILE"
echo ""

if [ "$CRITICAL_COUNT" -gt 0 ]; then
    echo -e "\033[31mRESULT: FAILED - Critical security issues found!\033[0m"
    exit 2
elif [ "$HIGH_COUNT" -gt 0 ]; then
    echo -e "\033[33mRESULT: WARNING - High severity issues found. Review recommended.\033[0m"
    exit 1
elif [ "$TOTAL_COUNT" -gt 0 ]; then
    echo -e "\033[36mRESULT: PASSED with notes - Minor issues found.\033[0m"
    exit 0
else
    echo -e "\033[32mRESULT: PASSED - No security issues detected!\033[0m"
    exit 0
fi
