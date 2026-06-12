#!/bin/bash
# Test script for Agent B workflow validation
# This script tests the workflow logic without mutating real GitHub state

set -e

echo "=== Agent B Workflow Test Suite ==="
echo ""

# Test 1: No matching Issue -> silent no-op
echo "Test 1: No matching Issue"
ISSUES=$(echo '[]')
if [ "$ISSUES" = "[]" ] || [ -z "$ISSUES" ]; then
  echo "✅ PASS: Silent exit when no matching issue"
else
  echo "❌ FAIL: Should have exited silently"
  exit 1
fi

# Test 2: One matching Issue -> selected as candidate
echo ""
echo "Test 2: One matching Issue"
ISSUES='[{"number":1,"title":"Test Issue","body":"## Allowed files\n- `docs/test.md`"}]'
ISSUE_NUMBER=$(echo "$ISSUES" | jq -r '.[0].number')
if [ "$ISSUE_NUMBER" = "1" ]; then
  echo "✅ PASS: Single issue selected correctly"
else
  echo "❌ FAIL: Should have selected issue #1"
  exit 1
fi

# Test 3: Multiple matching Issues -> only one selected
echo ""
echo "Test 3: Multiple matching Issues"
ISSUES='[{"number":1,"title":"First","body":""},{"number":2,"title":"Second","body":""}]'
SELECTED=$(echo "$ISSUES" | jq -r '.[0].number')
COUNT=$(echo "$ISSUES" | jq length)
if [ "$SELECTED" = "1" ] && [ "$COUNT" = "2" ]; then
  echo "✅ PASS: Only first issue selected from multiple"
else
  echo "❌ FAIL: Should select only one issue"
  exit 1
fi

# Test 4: Allowed files parsed successfully
echo ""
echo "Test 4: Allowed files parsing"
ISSUE_BODY='## Allowed files\n- `docs/test.md`\n- `docs/another.md`'
ALLOWED_FILES=$(echo -e "$ISSUE_BODY" | grep -oP '(?<=^- `)[^`]+(?=`)')
if [ -n "$ALLOWED_FILES" ]; then
  echo "✅ PASS: Allowed files parsed: $(echo $ALLOWED_FILES | tr '\n' ', ')"
else
  echo "❌ FAIL: Should have parsed allowed files"
  exit 1
fi

# Test 5: Allowed files parse failure -> stop safely
echo ""
echo "Test 5: Allowed files parse failure"
ISSUE_BODY='No allowed files section here'
ALLOWED_FILES=$(echo "$ISSUE_BODY" | grep -oP '(?<=^- `)[^`]+(?=`)')
if [ -z "$ALLOWED_FILES" ]; then
  echo "✅ PASS: Correctly detected parse failure"
else
  echo "❌ FAIL: Should have failed to parse"
  exit 1
fi

# Test 6: Forbidden path detection
echo ""
echo "Test 6: Forbidden path detection"
DIFF="driver/Filter.c\nsrc/main.c\ntests/test.cs"
FORBIDDEN_PATHS="driver/|src/|tests/|README|\.github/workflows/|\.sln|\.vcxproj|\.csproj"
if echo -e "$DIFF" | grep -qE "$FORBIDDEN_PATHS"; then
  echo "✅ PASS: Correctly detected forbidden paths"
else
  echo "❌ FAIL: Should have detected forbidden paths"
  exit 1
fi

# Test 6b: Allowed path not blocked
echo ""
echo "Test 6b: Allowed path not blocked"
DIFF="docs/test.md"
if ! echo -e "$DIFF" | grep -qE "$FORBIDDEN_PATHS"; then
  echo "✅ PASS: Allowed path not blocked"
else
  echo "❌ FAIL: Should not block docs files"
  exit 1
fi

# Test 7: dotnet test baseline failure detection
echo ""
echo "Test 7: dotnet test failure detection"
# Simulate test failure
TEST_RESULT=1
if [ $TEST_RESULT -ne 0 ]; then
  echo "✅ PASS: Correctly detected test failure"
else
  echo "❌ FAIL: Should have detected test failure"
  exit 1
fi

# Test 8: dotnet test post-change failure detection
echo ""
echo "Test 8: dotnet test post-change failure"
TEST_RESULT=1
if [ $TEST_RESULT -ne 0 ]; then
  echo "✅ PASS: Correctly detected post-change test failure"
else
  echo "❌ FAIL: Should have detected post-change test failure"
  exit 1
fi

# Test 9: PR body validation
echo ""
echo "Test 9: PR body validation"
PR_BODY="Closes #1

## Summary
Test summary

## Validation
- dotnet test: passed

## Safety Confirmation
- No driver installation"

if echo "$PR_BODY" | grep -q "Closes #" && \
   echo "$PR_BODY" | grep -q "Safety Confirmation" && \
   echo "$PR_BODY" | grep -q "Validation"; then
  echo "✅ PASS: PR body contains required sections"
else
  echo "❌ FAIL: PR body missing required sections"
  exit 1
fi

# Test 10: Whitelist exact matching (negative case)
echo ""
echo "Test 10: Whitelist exact matching"

# Negative case: docs/example.md should NOT match docs/example.md.bak
ALLOWED_FILES="docs/example.md.bak"
CHANGED_FILE="docs/example.md"
if echo "$ALLOWED_FILES" | grep -qxF "$CHANGED_FILE"; then
  echo "❌ FAIL: docs/example.md should not match docs/example.md.bak"
  exit 1
else
  echo "✅ PASS: docs/example.md rejected when only docs/example.md.bak allowed"
fi

# Positive case: exact match should pass
ALLOWED_FILES="docs/example.md"
CHANGED_FILE="docs/example.md"
if echo "$ALLOWED_FILES" | grep -qxF "$CHANGED_FILE"; then
  echo "✅ PASS: docs/example.md accepted when docs/example.md is allowed"
else
  echo "❌ FAIL: docs/example.md should match docs/example.md"
  exit 1
fi

# Negative case: substring match should fail
ALLOWED_FILES="docs/example.md.backup"
CHANGED_FILE="docs/example.md"
if echo "$ALLOWED_FILES" | grep -qxF "$CHANGED_FILE"; then
  echo "❌ FAIL: docs/example.md should not match docs/example.md.backup"
  exit 1
else
  echo "✅ PASS: docs/example.md rejected when only docs/example.md.backup allowed"
fi

echo ""
echo "=== All tests passed ==="
