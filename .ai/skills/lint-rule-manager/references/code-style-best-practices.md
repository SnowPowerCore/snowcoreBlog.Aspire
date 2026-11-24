# C# and XAML Code Style Best Practices Reference

This document is the research backing for the project's code style conventions. It provides full `.editorconfig` configuration, analyzer rule catalogs, and XAML conventions.

> **Enforced rules**: For the subset of rules actively enforced in this project (and CI behavior), see [`.ai/rules/code-style.md`](../../../rules/code-style.md).

---

## 1. C# Naming Conventions

Based on Microsoft/.NET Runtime guidelines:

| Element | Convention | Rule ID | Severity |
|---------|------------|---------|----------|
| **Classes/Structs/Enums** | PascalCase | IDE1006 | Error |
| **Interfaces** | `I` prefix + PascalCase | IDE1006 | Error |
| **Public Methods** | PascalCase | IDE1006 | Error |
| **Properties** | PascalCase | IDE1006 | Error |
| **Private Instance Fields** | `_camelCase` | IDE1006 | Error |
| **Private Static Fields** | `s_camelCase` | IDE1006 | Suggestion |
| **Thread Static Fields** | `t_camelCase` | IDE1006 | Suggestion |
| **Constants (const)** | PascalCase | IDE1006 | Suggestion |
| **Local Variables** | camelCase | IDE1006 | Suggestion |
| **Parameters** | camelCase | IDE1006 | Warning |
| **Type Parameters** | `T` prefix + PascalCase | IDE1006 | Warning |
| **Async Methods** | `*Async` suffix | SA1606 | Warning |

---

## 2. File Organization

### Using Directives

| Setting | Recommended Value | Rule ID |
|---------|-------------------|---------|
| Placement | Outside namespace | IDE0065 |
| Sort System.* first | true | - |
| Separate groups | true (optional) | - |

### Type Member Ordering (StyleCop SA1201-SA1204)

Recommended order within a class:
1. Constants
2. Fields
3. Constructors
4. Finalizers (Destructors)
5. Delegates
6. Events
7. Enums
8. Interfaces
9. Properties
10. Indexers
11. Methods
12. Structs
13. Classes

Within each group, order by access modifier:
1. `public`
2. `internal`
3. `protected internal`
4. `protected`
5. `private protected`
6. `private`
---

## 3. IDE Rules (Severity Recommendations)

### Must-Fix (Error)

| Rule ID | Description |
|---------|-------------|
| IDE0055 | Formatting violations (when configured) |
| IDE1006 | Naming rule violations (critical ones) |

### Should-Fix (Warning)

| Rule ID | Description |
|---------|-------------|
| IDE0001 | Simplify name |
| IDE0002 | Simplify member access |
| IDE0003 | Remove `this.` qualification |
| IDE0005 | Remove unnecessary using |
| IDE0016 | Use throw expression |
| IDE0017 | Use object initializer |
| IDE0018 | Inline variable declaration |
| IDE0019 | Use pattern matching (as/null check) |
| IDE0020 | Use pattern matching (is/cast check) |
| IDE0028 | Use collection initializer |
| IDE0029 | Use coalesce expression (non-nullable) |
| IDE0030 | Use coalesce expression (nullable) |
| IDE0031 | Use null propagation |
| IDE0032 | Use auto property |
| IDE0034 | Simplify default expression |
| IDE0036 | Order modifiers |
| IDE0040 | Add accessibility modifiers |
| IDE0044 | Add readonly modifier |
| IDE0051 | Remove unused private member |
| IDE0052 | Remove unread private member |
| IDE0060 | Remove unused parameter |
| IDE0065 | Using directive placement |

### Suggestion Only

| Rule ID | Description |
|---------|-------------|
| IDE0021-IDE0027 | Expression-bodied members |
| IDE0033 | Use explicitly provided tuple name |
| IDE0039 | Use local function |
| IDE0041 | Use is null check |
| IDE0046 | Use conditional expression |
| IDE0047 | Remove unnecessary parentheses |
| IDE0048 | Add parentheses for clarity |
| IDE0066 | Use switch expression |

---

## 4. CA (Code Analysis) Rules

### High Priority (Error/Warning)

| Rule ID | Description | Severity |
|---------|-------------|----------|
| CA1001 | Types with disposable fields should be disposable | Warning |
| CA1018 | Mark attributes with AttributeUsageAttribute | Warning |
| CA1036 | Override methods on comparable types | Warning |
| CA1051 | Do not declare visible instance fields | Warning |
| CA1062 | Validate arguments of public methods | Suggestion |
| CA1303 | Do not pass literals as localized parameters | None (often disabled) |
| CA1304 | Specify CultureInfo | Suggestion |
| CA1305 | Specify IFormatProvider | Suggestion |
| CA2000 | Dispose objects before losing scope | Warning |
| CA2007 | Do not directly await Task | Suggestion |
| CA2008 | Do not create tasks without passing TaskScheduler | Warning |
| CA2016 | Forward CancellationToken | Warning |

### Security Rules (Always Enable)

| Rule ID | Description | Severity |
|---------|-------------|----------|
| CA2100 | Review SQL queries for security vulnerabilities | Warning |
| CA2109 | Review visible event handlers | Warning |
| CA3001 | Review code for SQL injection vulnerabilities | Warning |
| CA3002 | Review code for XSS vulnerabilities | Warning |
| CA3003 | Review code for file path injection | Warning |
| CA3004 | Review code for information disclosure | Warning |
| CA5350 | Do not use weak cryptographic algorithms | Error |
| CA5351 | Do not use broken cryptographic algorithms | Error |

---

## 5. StyleCop.Analyzers Rules

### Rules to Disable (conflict with other settings)

| Rule ID | Description | Reason |
|---------|-------------|--------|
| SA1101 | Prefix local calls with this | Conflicts with IDE0003 |
| SA1200 | Using directives placement | Use IDE0065 instead |
| SA1309 | Field names should not begin with underscore | Conflicts with `_camelCase` convention |
| SA1413 | Use trailing comma | Optional, not enforced |
| SA1600-SA1602 | Elements should be documented | Too strict for most projects |
| SA1633 | File should have header | Use `file_header_template` instead |

### Rules to Enable

**Spacing** (SA1000-SA1028): SA1000, SA1001, SA1003, SA1005, SA1008, SA1009, SA1025

**Readability** (SA1100-SA1139): SA1116, SA1117, SA1118

**Ordering** (SA1200-SA1217): SA1201, SA1202, SA1203, SA1204

**Naming** (SA1300-SA1316): SA1300, SA1302, SA1303, SA1304, SA1306, SA1307, SA1310, SA1311

**Maintainability** (SA1400-SA1413): SA1400, SA1401, SA1402, SA1403

**Layout** (SA1500-SA1520): SA1500, SA1501, SA1502, SA1503, SA1504, SA1516, SA1520

---

## 6. XAML Best Practices

### Attribute Ordering (XAML Styler Default)

1. `x:Class`
2. `xmlns`, `xmlns:x`
3. `xmlns:*` (other namespaces)
4. `x:Key`, `Key`, `x:Name`, `Name`, `x:Uid`, `Uid`, `Title`
5. Grid positioning: `Grid.Row`, `Grid.RowSpan`, `Grid.Column`, `Grid.ColumnSpan`
6. Canvas positioning: `Canvas.Left`, `Canvas.Top`, etc.
7. Size: `Width`, `Height`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight`
8. Layout: `Margin`, `Padding`, `HorizontalAlignment`, `VerticalAlignment`
9. All other attributes (alphabetically)
10. Events

### XAML Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| `x:Name` | PascalCase (preferred over Name) | `x:Name="MainButton"` |
| `x:Key` for styles | Type + "Style" suffix | `x:Key="PrimaryButtonStyle"` |
| `x:Key` for templates | Type + "Template" suffix | `x:Key="ListItemTemplate"` |
| `x:Key` for converters | Descriptive + "Converter" suffix | `x:Key="BoolToVisibilityConverter"` |
| Resource dictionary files | PascalCase | `Colors.xaml`, `Styles.xaml` |

### XAML Tips

```xml
<!-- Use x:Name over Name -->
<Button x:Name="SubmitButton" />  <!-- Preferred -->
<Button Name="SubmitButton" />    <!-- Avoid -->

<!-- Use StaticResource over DynamicResource when possible -->
<Button Style="{StaticResource PrimaryButtonStyle}" />  <!-- Preferred -->
<Button Style="{DynamicResource PrimaryButtonStyle}" /> <!-- Only when needed -->

<!-- Use DataType in DataTemplates -->
<DataTemplate DataType="{x:Type local:Customer}">
    <TextBlock Text="{Binding Name}" />
</DataTemplate>

<!-- Use design-time data context -->
<UserControl d:DataContext="{d:DesignInstance Type=local:MainViewModel}">
```

### XAML Styler Configuration (`Settings.XamlStyler`)

```json
{
  "AttributesTolerance": 2,
  "KeepFirstAttributeOnSameLine": true,
  "MaxAttributeCharactersPerLine": 0,
  "MaxAttributesPerLine": 1,
  "NewlineExemptionElements": "RadialGradientBrush, GradientStop, LinearGradientBrush, ScaleTransform, SkewTransform, RotateTransform, TranslateTransform, Trigger, Condition, Setter",
  "SeparateByGroups": false,
  "EnableAttributeReordering": true,
  "AttributeOrderingRuleGroups": [
    "x:Class",
    "xmlns, xmlns:x",
    "xmlns:*",
    "x:Key, Key, x:Name, Name, x:Uid, Uid, Title",
    "Grid.Row, Grid.RowSpan, Grid.Column, Grid.ColumnSpan, Canvas.Left, Canvas.Top, Canvas.Right, Canvas.Bottom",
    "Width, Height, MinWidth, MinHeight, MaxWidth, MaxHeight",
    "Margin, Padding, HorizontalAlignment, VerticalAlignment, HorizontalContentAlignment, VerticalContentAlignment, Panel.ZIndex",
    "*:*, *",
    "PageSource, PageIndex, Offset, Color, TargetName, Property, Value, StartPoint, EndPoint",
    "mc:Ignorable, d:IsDataSource, d:LayoutOverrides, d:IsStaticText",
    "Storyboard.*, From, To, Duration"
  ],
  "OrderAttributesByName": true,
  "PutEndingBracketOnNewLine": false,
  "RemoveEndingTagOfEmptyElement": true,
  "SpaceBeforeClosingSlash": true,
  "FormatMarkupExtension": true,
  "NoNewLineMarkupExtensions": "x:Bind, Binding",
  "ThicknessStyle": 0,
  "ThicknessAttributes": "Margin, Padding, BorderThickness, ThumbnailClipMargin",
  "FormatOnSave": true,
  "CommentPadding": 2
}
```

---

## 7. Complete EditorConfig Template

Single source of truth for all `.editorconfig` values. Sections 1-6 above explain the rationale; this section provides the config.

```ini
# EditorConfig - https://editorconfig.org
root = true

###############################
# Core EditorConfig Options   #
###############################

[*]
charset = utf-8
indent_style = space
indent_size = 4
tab_width = 4
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

###############################
# XML/YAML Files              #
###############################

[*.{csproj,props,targets,config,nuspec,resx,ruleset,xml}]
indent_size = 2

[*.{yml,yaml,json}]
indent_size = 2

###############################
# C# Code Files               #
###############################

[*.cs]

#### .NET Formatting Options ####

# Using directives
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = true

# Braces - Allman style
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_between_query_expression_clauses = true

# Indentation
csharp_indent_block_contents = true
csharp_indent_braces = false
csharp_indent_case_contents = true
csharp_indent_case_contents_when_block = false
csharp_indent_switch_labels = true
csharp_indent_labels = one_less_than_current

# Spacing
csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_after_comma = true
csharp_space_after_dot = false
csharp_space_after_semicolon_in_for_statement = true
csharp_space_around_binary_operators = before_and_after
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_before_comma = false
csharp_space_before_dot = false
csharp_space_before_open_square_brackets = false
csharp_space_before_semicolon_in_for_statement = false
csharp_space_between_empty_square_brackets = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false
csharp_space_between_method_call_name_and_opening_parenthesis = false
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_declaration_name_and_open_parenthesis = false
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_parentheses = false
csharp_space_between_square_brackets = false

# Wrapping
csharp_preserve_single_line_blocks = true
csharp_preserve_single_line_statements = false

#### .NET Code Style ####

# this. preferences
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning

# Language keywords vs BCL types
dotnet_style_predefined_type_for_locals_parameters_members = true:warning
dotnet_style_predefined_type_for_member_access = true:warning

# Modifier preferences
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async:warning
dotnet_style_require_accessibility_modifiers = always:warning
dotnet_style_readonly_field = true:warning

# var preferences
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# Braces
csharp_prefer_braces = true:warning

# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_constructors = when_on_single_line:suggestion
csharp_style_expression_bodied_operators = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_indexers = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion
csharp_style_expression_bodied_lambdas = true:suggestion
csharp_style_expression_bodied_local_functions = when_on_single_line:suggestion

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
csharp_style_pattern_matching_over_as_with_null_check = true:warning
csharp_style_inlined_variable_declaration = true:warning
csharp_style_prefer_switch_expression = true:suggestion

# Null checking
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:warning

# Expression-level preferences
dotnet_style_object_initializer = true:warning
dotnet_style_collection_initializer = true:warning
dotnet_style_explicit_tuple_names = true:warning
dotnet_style_coalesce_expression = true:warning
dotnet_style_null_propagation = true:warning
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:warning
dotnet_style_prefer_auto_properties = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:suggestion
dotnet_style_prefer_conditional_expression_over_return = true:suggestion
csharp_prefer_simple_default_expression = true:warning

# Index/range operators
csharp_style_prefer_index_operator = true:suggestion
csharp_style_prefer_range_operator = true:suggestion

# Misc preferences
csharp_style_prefer_static_local_function = true:suggestion
csharp_prefer_simple_using_statement = true:suggestion

#### Naming Conventions ####

# Private fields: _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case.severity = error
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_underscore_prefix

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_underscore_prefix.capitalization = camel_case
dotnet_naming_style.camel_case_underscore_prefix.required_prefix = _

# Interfaces: IInterfaceName
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.severity = error
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_should_be_prefixed_with_i.style = interface_style

dotnet_naming_symbols.interfaces.applicable_kinds = interface

dotnet_naming_style.interface_style.capitalization = pascal_case
dotnet_naming_style.interface_style.required_prefix = I

# Static fields: s_camelCase
dotnet_naming_rule.static_fields_should_have_prefix.severity = suggestion
dotnet_naming_rule.static_fields_should_have_prefix.symbols = static_fields
dotnet_naming_rule.static_fields_should_have_prefix.style = static_prefix_style

dotnet_naming_symbols.static_fields.applicable_kinds = field
dotnet_naming_symbols.static_fields.required_modifiers = static
dotnet_naming_symbols.static_fields.applicable_accessibilities = private, internal, private_protected

dotnet_naming_style.static_prefix_style.required_prefix = s_
dotnet_naming_style.static_prefix_style.capitalization = camel_case

# Constants: PascalCase
dotnet_naming_rule.constant_fields_should_be_pascal_case.severity = suggestion
dotnet_naming_rule.constant_fields_should_be_pascal_case.symbols = constant_fields
dotnet_naming_rule.constant_fields_should_be_pascal_case.style = pascal_case_style

dotnet_naming_symbols.constant_fields.applicable_kinds = field
dotnet_naming_symbols.constant_fields.required_modifiers = const

dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# Public members: PascalCase
dotnet_naming_rule.public_members_must_be_capitalized.severity = warning
dotnet_naming_rule.public_members_must_be_capitalized.symbols = public_symbols
dotnet_naming_rule.public_members_must_be_capitalized.style = pascal_case_style

dotnet_naming_symbols.public_symbols.applicable_kinds = property, method, field, event, delegate, class, struct, interface, enum
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public

# Async methods: *Async suffix
dotnet_naming_rule.async_methods_must_end_with_async.severity = warning
dotnet_naming_rule.async_methods_must_end_with_async.symbols = async_methods
dotnet_naming_rule.async_methods_must_end_with_async.style = async_suffix_style

dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async

dotnet_naming_style.async_suffix_style.capitalization = pascal_case
dotnet_naming_style.async_suffix_style.required_suffix = Async

# Type parameters: T prefix
dotnet_naming_rule.type_parameters_should_be_prefixed_with_t.severity = warning
dotnet_naming_rule.type_parameters_should_be_prefixed_with_t.symbols = type_parameters
dotnet_naming_rule.type_parameters_should_be_prefixed_with_t.style = type_parameter_style

dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter

dotnet_naming_style.type_parameter_style.required_prefix = T
dotnet_naming_style.type_parameter_style.capitalization = pascal_case

# Parameters: camelCase
dotnet_naming_rule.parameters_should_be_camel_case.severity = warning
dotnet_naming_rule.parameters_should_be_camel_case.symbols = parameters
dotnet_naming_rule.parameters_should_be_camel_case.style = camel_case_style

dotnet_naming_symbols.parameters.applicable_kinds = parameter

dotnet_naming_style.camel_case_style.capitalization = camel_case

# Local variables: camelCase
dotnet_naming_rule.local_variables_should_be_camel_case.severity = suggestion
dotnet_naming_rule.local_variables_should_be_camel_case.symbols = local_variables
dotnet_naming_rule.local_variables_should_be_camel_case.style = camel_case_style

dotnet_naming_symbols.local_variables.applicable_kinds = local

#### IDE Analyzers ####

dotnet_diagnostic.IDE0055.severity = warning
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.IDE0051.severity = warning
dotnet_diagnostic.IDE0052.severity = warning
dotnet_diagnostic.IDE0060.severity = suggestion

#### CA Analyzers ####

dotnet_diagnostic.CA1001.severity = warning
dotnet_diagnostic.CA2000.severity = warning
dotnet_diagnostic.CA2007.severity = suggestion
dotnet_diagnostic.CA2016.severity = warning

#### Nullable Reference Types ####

dotnet_diagnostic.CS8600.severity = warning
dotnet_diagnostic.CS8601.severity = warning
dotnet_diagnostic.CS8602.severity = warning
dotnet_diagnostic.CS8603.severity = warning
dotnet_diagnostic.CS8604.severity = warning
dotnet_diagnostic.CS8618.severity = warning

#### StyleCop ####

# Disabled (conflicts)
dotnet_diagnostic.SA1101.severity = none
dotnet_diagnostic.SA1200.severity = none
dotnet_diagnostic.SA1309.severity = none
dotnet_diagnostic.SA1600.severity = none
dotnet_diagnostic.SA1633.severity = none

# Enabled
dotnet_diagnostic.SA1000.severity = warning
dotnet_diagnostic.SA1001.severity = warning
dotnet_diagnostic.SA1201.severity = warning
dotnet_diagnostic.SA1202.severity = warning
dotnet_diagnostic.SA1500.severity = warning
dotnet_diagnostic.SA1503.severity = warning

###############################
# XAML Files                  #
###############################

[*.xaml]
indent_size = 4
```

---

## Sources

- [Microsoft .NET Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- [dotnet format CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [XAML Styler Wiki](https://github.com/Xavalon/XamlStyler/wiki)
- [Code-style rules overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/)
