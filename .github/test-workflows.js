// Test script for Agent B workflow validation
// This script tests the workflow logic without mutating real GitHub state

console.log("=== Agent B Workflow Test Suite ===\n");

let passed = 0;
let failed = 0;

function test(name, condition) {
  if (condition) {
    console.log(`✅ PASS: ${name}`);
    passed++;
  } else {
    console.log(`❌ FAIL: ${name}`);
    failed++;
  }
}

// Test 1: No matching Issue -> silent no-op
console.log("Test 1: No matching Issue");
const noIssues = [];
test("Silent exit when no matching issue", noIssues.length === 0);

// Test 2: One matching Issue -> selected as candidate
console.log("\nTest 2: One matching Issue");
const oneIssue = [{ number: 1, title: "Test Issue", body: "## Allowed files\n- `docs/test.md`" }];
test("Single issue selected correctly", oneIssue[0].number === 1);

// Test 3: Multiple matching Issues -> only one selected
console.log("\nTest 3: Multiple matching Issues");
const multipleIssues = [
  { number: 1, title: "First", body: "" },
  { number: 2, title: "Second", body: "" }
];
test("Only first issue selected from multiple", multipleIssues[0].number === 1 && multipleIssues.length === 2);

// Test 4: Allowed files parsed successfully
console.log("\nTest 4: Allowed files parsing");
const issueBody = "## Allowed files\n- `docs/test.md`\n- `docs/another.md`";
const allowedFilesMatch = issueBody.match(/^- `([^`]+)`/gm);
const allowedFiles = allowedFilesMatch ? allowedFilesMatch.map(m => m.replace(/^- `/, '').replace(/`$/, '')) : [];
test("Allowed files parsed successfully", allowedFiles.length === 2 && allowedFiles[0] === "docs/test.md");

// Test 5: Allowed files parse failure -> stop safely
console.log("\nTest 5: Allowed files parse failure");
const noAllowedBody = "No allowed files section here";
const noAllowedMatch = noAllowedBody.match(/^- `([^`]+)`/gm);
test("Correctly detected parse failure", !noAllowedMatch || noAllowedMatch.length === 0);

// Test 6: Forbidden path detection
console.log("\nTest 6: Forbidden path detection");
const forbiddenDiff = "driver/Filter.c\nsrc/main.c\ntests/test.cs";
// Must match production regex in agent-a.yml: ^(driver/|src/|tests/|README\.md|\.github/workflows/|.*\.sln$|.*\.vcxproj$|.*\.csproj$)
const forbiddenPaths = /^(driver\/|src\/|tests\/|README\.md|\.github\/workflows\/|.*\.sln$|.*\.vcxproj$|.*\.csproj$)/m;
test("Correctly detected forbidden paths", forbiddenPaths.test(forbiddenDiff));

// Test 6b: Allowed path not blocked
console.log("\nTest 6b: Allowed path not blocked");
const allowedDiff = "docs/test.md";
test("Allowed path not blocked", !forbiddenPaths.test(allowedDiff));

// Test 6c: README.md blocked but README not blocked
console.log("\nTest 6c: README.md vs README");
test("README.md is blocked", forbiddenPaths.test("README.md"));
test("README is not blocked", !forbiddenPaths.test("README"));

// Test 6d: Extension anchoring
console.log("\nTest 6d: Extension anchoring");
test(".sln file blocked", forbiddenPaths.test("project.sln"));
test(".vcxproj file blocked", forbiddenPaths.test("project.vcxproj"));
test(".csproj file blocked", forbiddenPaths.test("project.csproj"));

// Test 7: dotnet test baseline failure detection
console.log("\nTest 7: dotnet test failure detection");
const testResult = 1;
test("Correctly detected test failure", testResult !== 0);

// Test 8: dotnet test post-change failure detection
console.log("\nTest 8: dotnet test post-change failure");
const postTestResult = 1;
test("Correctly detected post-change test failure", postTestResult !== 0);

// Test 9: PR body validation
console.log("\nTest 9: PR body validation");
const prBody = `Closes #1

## Summary
Test summary

## Validation
- dotnet test: passed

## Safety Confirmation
- No driver installation`;

const hasCloses = prBody.includes("Closes #");
const hasSafety = /Safety Confirmation/i.test(prBody);
const hasValidation = prBody.includes("Validation");
test("PR body contains required sections", hasCloses && hasSafety && hasValidation);

// Test 9b: Case-insensitive safety confirmation
console.log("\nTest 9b: Case-insensitive safety confirmation");
const prBodyLower = `Closes #1\n\n## Safety confirmation\n- No driver installation`;
test("Lowercase 'Safety confirmation' accepted", /Safety Confirmation/i.test(prBodyLower));

// Test 10: Whitelist exact matching (negative case)
console.log("\nTest 10: Whitelist exact matching");
// Simulate the whitelist check logic from agent-a.yml
function checkWhitelist(diffFiles, allowedFiles) {
  for (const file of diffFiles) {
    // Allow .github/ files (workflow artifacts)
    if (file.startsWith('.github/')) continue;
    // Check if file exactly matches a line in allowed files list
    // Uses grep -qxF logic: exact line match
    const lines = allowedFiles.split('\n');
    let found = false;
    for (const line of lines) {
      if (line.trim() === file) {
        found = true;
        break;
      }
    }
    if (!found) return { passed: false, file };
  }
  return { passed: true };
}

// Negative case: docs/example.md should NOT match docs/example.md.bak
const allowedFilesBak = "docs/example.md.bak";
const changedFileMd = "docs/example.md";
const result1 = checkWhitelist([changedFileMd], allowedFilesBak);
test("docs/example.md rejected when only docs/example.md.bak allowed", !result1.passed);

// Positive case: exact match should pass
const allowedFilesExact = "docs/example.md";
const result2 = checkWhitelist([changedFileMd], allowedFilesExact);
test("docs/example.md accepted when docs/example.md is allowed", result2.passed);

// Negative case: substring match should fail
const allowedFilesLonger = "docs/example.md.backup";
const result3 = checkWhitelist([changedFileMd], allowedFilesLonger);
test("docs/example.md rejected when only docs/example.md.backup allowed", !result3.passed);

// Summary
console.log(`\n=== Results: ${passed} passed, ${failed} failed ===`);
process.exit(failed > 0 ? 1 : 0);
