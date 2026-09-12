## Task scope and completion

- Use the requested outcome and repository constraints to decide what is needed. Read supporting docs and skills only when they apply to the change; a small edit does not require a full repository survey.
- Continue authorized work through implementation and proportionate verification, fixing issues introduced by the change before handing off. Make routine, reversible local decisions without asking again. Ask when missing information materially affects correctness or an action exceeds the authorized scope; identify the specific boundary.
- Choose validation based on risk. Small documentation or low-risk edits do not automatically need builds or tests. For substantial or risky changes, use the smallest relevant checks and rerun affected checks after fixes. Report what was checked and any remaining gaps.

## Test execution

If you need to execute specific tests, use Microsoft Testing Platform (MTP) filters, not VSTest filters.

Do not use:

```bash
dotnet test --filter ...
```

Use:

```bash
dotnet test --project <project-directory-or-csproj> -- --treenode-filter "<filter>"
```

Format:

```text
/<Assembly>/<Namespace>/<Class>/<Test>
```

Examples:

```bash
dotnet test --project <project-directory-or-csproj> -- --treenode-filter "/*/*/*/MyTest"
dotnet test --project <project-directory-or-csproj> -- --treenode-filter "/*/*/MyTestClass/*"
```