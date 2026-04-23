## Test Execution (TUnit + Microsoft.Testing.Platform)

This repository uses **TUnit on Microsoft.Testing.Platform (MTP)**.

### ❌ Do NOT use

```bash
dotnet test --filter ...
````

This is not supported and may result in zero tests running.

---

### ✅ Use this instead

```bash
dotnet test -- --treenode-filter "<filter>"
```

The `--` is required.

---

### Filter format

```
/<Assembly>/<Namespace>/<Class>/<Test>
```

Wildcards (`*`) are supported.

---

### Examples

Single test:

```bash
dotnet test -- --treenode-filter "/*/*/*/MyTest"
```

Class:

```bash
dotnet test -- --treenode-filter "/*/*/MyTestClass/*"
```
