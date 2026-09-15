#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Sinh bảng tra API dạng Markdown cho AI từ chính assembly runtime đang nạp của GameUp Core.
    /// Chữ ký lấy bằng reflection nên luôn khớp đúng bản Core đang cài, không lệch như tài liệu viết tay;
    /// summary và đường dẫn file lấy từ source để AI biết mở file nào khi cần chi tiết.
    /// </summary>
    public static class GUApiIndexBuilder
    {
        private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private const int MaxOverridableListed = 10;

        private static readonly Regex TypeDeclaration = new Regex(
            @"^\s*(?:(?:public|internal|protected|private|static|abstract|sealed|partial|readonly|unsafe|new)\s+)*(?:class|struct|interface|enum|record)\s+(?<name>[A-Za-z_]\w*)",
            RegexOptions.Compiled);

        private static readonly Regex DelegateDeclaration = new Regex(
            @"^\s*(?:(?:public|internal|protected|private|new)\s+)*delegate\s+[\w<>\[\],.?\s]+?\s+(?<name>[A-Za-z_]\w*)\s*[<(]",
            RegexOptions.Compiled);

        private static readonly Regex XmlReference = new Regex(
            @"<(?:see|seealso|paramref|typeparamref)\s+(?:cref|name)=""(?<ref>[^""]+)""\s*/>",
            RegexOptions.Compiled);

        private static readonly Regex XmlTag = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private static readonly Dictionary<Type, string> Keywords = new Dictionary<Type, string>
        {
            { typeof(void), "void" }, { typeof(object), "object" }, { typeof(string), "string" },
            { typeof(bool), "bool" }, { typeof(byte), "byte" }, { typeof(sbyte), "sbyte" },
            { typeof(char), "char" }, { typeof(short), "short" }, { typeof(ushort), "ushort" },
            { typeof(int), "int" }, { typeof(uint), "uint" }, { typeof(long), "long" },
            { typeof(ulong), "ulong" }, { typeof(float), "float" }, { typeof(double), "double" },
            { typeof(decimal), "decimal" }
        };

        /// <summary>Vị trí khai báo type trong source: file tương đối so với gốc package + summary.</summary>
        private readonly struct Declaration
        {
            public readonly string RelativePath;
            public readonly string Summary;

            public Declaration(string relativePath, string summary)
            {
                RelativePath = relativePath;
                Summary = summary;
            }
        }

        /// <summary>
        /// Dựng nội dung <c>API_INDEX.md</c> cho một package GameUp (Core, SDK, IAP…).
        /// </summary>
        /// <param name="displayName">Tên hiển thị ở tiêu đề, vd <c>GameUp SDK</c>.</param>
        /// <param name="packageName">Tên UPM, vd <c>com.ohze.gameup.sdk</c>.</param>
        /// <param name="assetRoot">Đường dẫn ảo Unity của package: <c>Packages/com.ohze.gameup.sdk</c> hoặc <c>Assets/GameUpSDK</c>.</param>
        /// <param name="diskRoot">Thư mục thật chứa package (có thể nằm trong Library/PackageCache).</param>
        /// <param name="readRoot">Thư mục AI được phép đọc source, tương đối so với gốc project.</param>
        /// <param name="version">Version package, in ở đầu file.</param>
        public static string Build(string displayName, string packageName, string assetRoot, string diskRoot, string readRoot, string version)
        {
            var prefix = assetRoot.TrimEnd('/') + "/";
            var types = new List<Type>();
            var assemblyNamespaces = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var sourceFiles = new List<string>();

            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (!IsRuntimeAssemblyOfPackage(assembly, prefix))
                    continue;

                sourceFiles.AddRange(assembly.sourceFiles);

                var loaded = FindLoadedAssembly(assembly.name);
                if (loaded == null)
                    continue;

                var namespaces = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var type in GetExportedTypes(loaded))
                {
                    types.Add(type);
                    namespaces.Add(type.Namespace ?? "(global)");
                }

                assemblyNamespaces[assembly.name] = namespaces;
            }

            var declarations = ParseDeclarations(sourceFiles, prefix, diskRoot);
            var sb = new StringBuilder();

            AppendHeader(sb, displayName, packageName, readRoot, version, assetRoot);
            AppendAssemblies(sb, assemblyNamespaces);
            AppendBaseClasses(sb, types, declarations, readRoot);
            AppendPrefabs(sb, diskRoot, prefix);
            AppendTypesByFolder(sb, types, declarations, readRoot);

            return sb.ToString();
        }

        // ─── Thu thập ────────────────────────────────────────────────────────

        /// <summary>Chỉ assembly runtime của Core — Editor tooling và Tests là nhiễu với người viết game.</summary>
        private static bool IsRuntimeAssemblyOfPackage(UnityEditor.Compilation.Assembly assembly, string prefix)
        {
            if ((assembly.flags & UnityEditor.Compilation.AssemblyFlags.EditorAssembly) != 0)
                return false;

            if (assembly.name.IndexOf("Tests", StringComparison.Ordinal) >= 0)
                return false;

            return assembly.sourceFiles.Any(file => file.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static System.Reflection.Assembly FindLoadedAssembly(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == name)
                    return assembly;
            }

            return null;
        }

        private static IEnumerable<Type> GetExportedTypes(System.Reflection.Assembly assembly)
        {
            Type[] all;
            try
            {
                all = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                all = e.Types.Where(type => type != null).ToArray();
            }

            return all
                .Where(type => (type.IsPublic || type.IsNestedPublic) && !IsCompilerGenerated(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
        }

        private static Dictionary<string, Declaration> ParseDeclarations(IEnumerable<string> assetPaths, string prefix, string diskRoot)
        {
            var result = new Dictionary<string, Declaration>(StringComparer.Ordinal);

            foreach (var assetPath in assetPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var relative = assetPath.Substring(prefix.Length);
                var diskPath = Path.Combine(diskRoot, relative);
                if (!File.Exists(diskPath))
                    continue;

                var lines = File.ReadAllLines(diskPath);
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = TypeDeclaration.Match(lines[i]);
                    if (!match.Success)
                        match = DelegateDeclaration.Match(lines[i]);
                    if (!match.Success)
                        continue;

                    var name = match.Groups["name"].Value;
                    if (!result.ContainsKey(name))
                        result[name] = new Declaration(relative.Replace('\\', '/'), ReadSummaryAbove(lines, i));
                }
            }

            return result;
        }

        /// <summary>Lấy nội dung <c>&lt;summary&gt;</c> ngay trên dòng khai báo, bỏ qua attribute và chỉ thị <c>#if</c>.</summary>
        private static string ReadSummaryAbove(string[] lines, int declarationLine)
        {
            var docLines = new List<string>();
            for (var i = declarationLine - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("///", StringComparison.Ordinal))
                {
                    docLines.Add(line.Substring(3));
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                break;
            }

            if (docLines.Count == 0)
                return string.Empty;

            docLines.Reverse();
            var doc = string.Join(" ", docLines);

            const string open = "<summary>";
            var start = doc.IndexOf(open, StringComparison.Ordinal);
            var end = doc.IndexOf("</summary>", StringComparison.Ordinal);
            if (start < 0 || end <= start)
                return string.Empty;

            doc = doc.Substring(start + open.Length, end - start - open.Length);
            doc = XmlReference.Replace(doc, "`${ref}`");
            doc = XmlTag.Replace(doc, string.Empty);
            return Whitespace.Replace(doc, " ").Trim();
        }

        // ─── Các phần của file ───────────────────────────────────────────────

        private static void AppendHeader(StringBuilder sb, string displayName, string packageName, string readRoot, string version, string assetRoot)
        {
            sb.AppendLine($"# {displayName} — API index (tự sinh)");
            sb.AppendLine();
            sb.AppendLine($"> **Không sửa tay.** Sinh bởi `{GUCoreSourceMirror.MenuPath.Replace("/", " → ")}` từ assembly thật của `{packageName}` `{version}`; lần sync sau sẽ ghi đè.");
            sb.AppendLine();
            sb.AppendLine($"- Source đọc được: `{readRoot}/` — cột **File** bên dưới là đường dẫn tương đối so với thư mục này.");
            sb.AppendLine($"- Đường dẫn trong Unity (dùng cho asmdef/AssetDatabase): `{assetRoot}/`.");
            sb.AppendLine("- Chữ ký lấy bằng reflection → khớp bản đang cài. Hành vi, comment, ví dụ → mở file nguồn.");
            sb.AppendLine("- Chỉ liệt kê member `public`/`protected` và field `[SerializeField]`. `[Obsolete]` = đừng dùng cho code mới.");
            sb.AppendLine();
        }

        private static void AppendAssemblies(StringBuilder sb, SortedDictionary<string, SortedSet<string>> assemblyNamespaces)
        {
            sb.AppendLine("## Assembly (asmdef cần reference)");
            sb.AppendLine();
            sb.AppendLine("| asmdef | Namespace |");
            sb.AppendLine("|---|---|");
            foreach (var pair in assemblyNamespaces)
                sb.AppendLine($"| `{pair.Key}` | {string.Join(", ", pair.Value.Select(ns => $"`{ns}`"))} |");
            sb.AppendLine();
        }

        private static void AppendBaseClasses(StringBuilder sb, List<Type> types, Dictionary<string, Declaration> declarations, string readRoot)
        {
            sb.AppendLine("## Class nền để kế thừa");
            sb.AppendLine();
            sb.AppendLine("Kế thừa những class này thay vì tự viết lại. **abstract** = bắt buộc override; **virtual** = hook tuỳ chọn (nhớ gọi `base.` nếu class nền có logic).");
            sb.AppendLine();
            sb.AppendLine("| Type | Namespace | abstract | virtual | File |");
            sb.AppendLine("|---|---|---|---|---|");

            var packageAssemblies = new HashSet<System.Reflection.Assembly>(types.Select(type => type.Assembly));

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsSealed || IsDelegate(type))
                    continue;

                var abstractMembers = new List<string>();
                var virtualMembers = new List<string>();
                CollectOverridableMembers(type, packageAssemblies, abstractMembers, virtualMembers);
                if (abstractMembers.Count == 0 && virtualMembers.Count == 0 && !type.IsAbstract)
                    continue;

                sb.AppendLine(
                    $"| `{FormatType(type)}` | `{type.Namespace}` | {FormatNameList(abstractMembers)} | {FormatNameList(virtualMembers)} | {FormatFileLink(type, declarations, readRoot)} |");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Gồm cả member kế thừa: <c>UIScreen</c>/<c>UIPopup</c> không khai báo virtual mới nhưng là class hay kế thừa nhất.
        /// Virtual gốc từ Unity/BCL (<c>ToString</c>, <c>Equals</c>…) bị bỏ vì không phải extension point của Core.
        /// </summary>
        private static void CollectOverridableMembers(Type type, HashSet<System.Reflection.Assembly> packageAssemblies,
            List<string> abstractMembers, List<string> virtualMembers)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods.OrderBy(m => m.MetadataToken))
            {
                if (!IsVisible(method) || !method.IsVirtual || method.IsFinal)
                    continue;
                if (!packageAssemblies.Contains(method.GetBaseDefinition().DeclaringType.Assembly))
                    continue;

                var name = method.IsSpecialName ? StripAccessorPrefix(method.Name) : $"{method.Name}()";
                var target = method.IsAbstract ? abstractMembers : virtualMembers;
                if (!target.Contains(name))
                    target.Add(name);
            }
        }

        private static void AppendPrefabs(StringBuilder sb, string diskRoot, string prefix)
        {
            var prefabDir = Path.Combine(diskRoot, "Prefab");
            if (!Directory.Exists(prefabDir))
                return;

            var prefabs = Directory.GetFiles(prefabDir, "*.prefab", SearchOption.AllDirectories)
                .Select(path => prefix + path.Substring(diskRoot.Length).TrimStart('/', '\\').Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (prefabs.Count == 0)
                return;

            sb.AppendLine("## Prefab có sẵn trong package");
            sb.AppendLine();
            sb.AppendLine("Không chép sang thư mục source (file YAML lớn). Prefab trong package cài qua UPM là read-only — dùng tool setup của package (hoặc Prefab Variant trong `_MainProject`), không sửa bản gốc.");
            sb.AppendLine();
            foreach (var prefab in prefabs)
                sb.AppendLine($"- `{prefab}`");
            sb.AppendLine();
        }

        private static void AppendTypesByFolder(StringBuilder sb, List<Type> types, Dictionary<string, Declaration> declarations, string readRoot)
        {
            var groups = types
                .GroupBy(type => GetFolder(type, declarations))
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();

                foreach (var type in group)
                    AppendType(sb, type, declarations, readRoot);
            }
        }

        private static void AppendType(StringBuilder sb, Type type, Dictionary<string, Declaration> declarations, string readRoot)
        {
            sb.AppendLine($"### `{FormatTypeHeader(type)}`");
            sb.AppendLine();
            sb.AppendLine($"`{type.Namespace ?? "(global)"}` · {FormatFileLink(type, declarations, readRoot)}");

            if (TryGetDeclaration(type, declarations, out var declaration) && declaration.Summary.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"> {declaration.Summary}");
            }

            sb.AppendLine();

            var lines = BuildMemberLines(type);
            foreach (var line in lines)
                sb.AppendLine($"- `{line}`");

            if (lines.Count > 0)
                sb.AppendLine();
        }

        // ─── Member ──────────────────────────────────────────────────────────

        private static List<string> BuildMemberLines(Type type)
        {
            var lines = new List<string>();

            if (type.IsEnum)
            {
                lines.Add(string.Join(", ", Enum.GetNames(type)));
                return lines;
            }

            if (IsDelegate(type))
                return lines;

            var isInterface = type.IsInterface;

            foreach (var field in type.GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken))
            {
                if (IsCompilerGenerated(field) || field.IsSpecialName)
                    continue;

                var serialized = field.IsPrivate && field.IsDefined(typeof(SerializeField), false);
                if (!serialized && !IsVisible(field))
                    continue;

                lines.Add(FormatField(field, serialized));
            }

            foreach (var property in type.GetProperties(DeclaredMembers).OrderBy(p => p.MetadataToken))
            {
                var line = FormatProperty(property, isInterface);
                if (line != null)
                    lines.Add(line);
            }

            foreach (var evt in type.GetEvents(DeclaredMembers).OrderBy(e => e.MetadataToken))
            {
                var add = evt.GetAddMethod(true);
                if (add == null || !IsVisible(add))
                    continue;

                lines.Add($"{ObsoletePrefix(evt)}{FormatMethodModifiers(add, isInterface)}event {FormatType(evt.EventHandlerType)} {evt.Name}");
            }

            if (!type.IsAbstract || !type.IsSealed)
            {
                foreach (var ctor in type.GetConstructors(DeclaredMembers).OrderBy(c => c.MetadataToken))
                {
                    if (ctor.IsStatic || !IsVisible(ctor) || ctor.GetParameters().Length == 0)
                        continue;

                    lines.Add($"{ObsoletePrefix(ctor)}{Access(ctor)} {CleanName(type.Name)}({FormatParameters(ctor)})");
                }
            }

            foreach (var method in type.GetMethods(DeclaredMembers).OrderBy(m => m.MetadataToken))
            {
                if (method.IsSpecialName || IsCompilerGenerated(method) || !IsVisible(method))
                    continue;

                lines.Add(FormatMethod(method, isInterface));
            }

            return lines;
        }

        private static string FormatField(FieldInfo field, bool serialized)
        {
            var modifiers = serialized ? "[SerializeField] private" : Access(field);
            if (field.IsLiteral)
                modifiers += " const";
            else
            {
                if (field.IsStatic)
                    modifiers += " static";
                if (field.IsInitOnly)
                    modifiers += " readonly";
            }

            return $"{ObsoletePrefix(field)}{modifiers} {FormatType(field.FieldType)} {field.Name}";
        }

        private static string FormatProperty(PropertyInfo property, bool isInterface)
        {
            var getter = property.GetGetMethod(true);
            var setter = property.GetSetMethod(true);
            var visibleGetter = getter != null && IsVisible(getter);
            var visibleSetter = setter != null && IsVisible(setter);
            if (!visibleGetter && !visibleSetter)
                return null;

            var main = visibleGetter ? getter : setter;
            var accessors = new StringBuilder("{ ");
            if (visibleGetter)
                accessors.Append("get; ");
            if (visibleSetter)
                accessors.Append(!isInterface && setter.IsFamily && main.IsPublic ? "protected set; " : "set; ");
            accessors.Append('}');

            var indexParameters = property.GetIndexParameters();
            var name = indexParameters.Length > 0
                ? $"this[{string.Join(", ", indexParameters.Select(FormatParameter))}]"
                : property.Name;

            return $"{ObsoletePrefix(property)}{FormatMethodModifiers(main, isInterface)}{FormatType(property.PropertyType)} {name} {accessors}";
        }

        private static string FormatMethod(MethodInfo method, bool isInterface)
        {
            var name = method.Name;
            if (method.IsGenericMethodDefinition)
                name += $"<{string.Join(", ", method.GetGenericArguments().Select(FormatType))}>";

            var parameters = FormatParameters(method);
            if (method.IsDefined(typeof(ExtensionAttribute), false))
                parameters = $"this {parameters}";

            return $"{ObsoletePrefix(method)}{FormatMethodModifiers(method, isInterface)}{FormatType(method.ReturnType)} {name}({parameters})";
        }

        private static string FormatMethodModifiers(MethodInfo method, bool isInterface)
        {
            if (isInterface)
                return string.Empty;

            var modifiers = new StringBuilder(Access(method));
            if (method.IsStatic)
                modifiers.Append(" static");
            else if (method.IsAbstract)
                modifiers.Append(" abstract");
            else if (method.IsVirtual)
            {
                var isOverride = method.GetBaseDefinition().DeclaringType != method.DeclaringType;
                if (!method.IsFinal)
                    modifiers.Append(isOverride ? " override" : " virtual");
                else if (isOverride)
                    modifiers.Append(" sealed override");
            }

            modifiers.Append(' ');
            return modifiers.ToString();
        }

        private static string FormatParameters(MethodBase method)
        {
            return string.Join(", ", method.GetParameters().Select(FormatParameter));
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            var prefix = string.Empty;
            if (type.IsByRef)
            {
                prefix = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
                type = type.GetElementType();
            }
            else if (parameter.IsDefined(typeof(ParamArrayAttribute), false))
            {
                prefix = "params ";
            }

            var text = $"{prefix}{FormatType(type)} {parameter.Name}";
            return TryFormatDefaultValue(parameter, type, out var value) ? $"{text} = {value}" : text;
        }

        private static bool TryFormatDefaultValue(ParameterInfo parameter, Type type, out string value)
        {
            value = null;
            try
            {
                if (!parameter.HasDefaultValue)
                    return false;
            }
            catch (FormatException)
            {
                return false;
            }

            var raw = parameter.DefaultValue;
            if (raw == null)
                value = type.IsValueType && Nullable.GetUnderlyingType(type) == null ? "default" : "null";
            else if (type.IsEnum)
                value = $"{FormatType(type)}.{Enum.ToObject(type, raw)}";
            else if (raw is string text)
                value = $"\"{text}\"";
            else if (raw is bool flag)
                value = flag ? "true" : "false";
            else if (raw is float single)
                value = $"{single.ToString(CultureInfo.InvariantCulture)}f";
            else
                value = Convert.ToString(raw, CultureInfo.InvariantCulture);

            return true;
        }

        // ─── Định dạng type ──────────────────────────────────────────────────

        /// <summary>Tên type theo cú pháp C# (<c>Dictionary&lt;string, int[]&gt;</c>, <c>int?</c>) thay vì tên CLR (<c>Dictionary`2</c>).</summary>
        public static string FormatType(Type type)
        {
            if (type.IsByRef)
                return FormatType(type.GetElementType());

            if (type.IsArray)
                return $"{FormatType(type.GetElementType())}[{new string(',', type.GetArrayRank() - 1)}]";

            if (type.IsGenericParameter)
                return type.Name;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return $"{FormatType(underlying)}?";

            if (Keywords.TryGetValue(type, out var keyword))
                return keyword;

            var name = CleanName(type.Name);
            var arguments = type.GetGenericArguments();
            var inherited = type.IsNested ? type.DeclaringType.GetGenericArguments().Length : 0;
            if (arguments.Length > inherited)
                name += $"<{string.Join(", ", arguments.Skip(inherited).Select(FormatType))}>";

            return type.IsNested ? $"{FormatType(type.DeclaringType)}.{name}" : name;
        }

        private static string FormatTypeHeader(Type type)
        {
            if (IsDelegate(type))
            {
                var invoke = type.GetMethod("Invoke");
                return invoke == null
                    ? $"public delegate {FormatType(type)}"
                    : $"public delegate {FormatType(invoke.ReturnType)} {FormatType(type)}({FormatParameters(invoke)})";
            }

            string kind;
            if (type.IsInterface)
                kind = "interface";
            else if (type.IsEnum)
                kind = "enum";
            else if (type.IsValueType)
                kind = "struct";
            else if (type.IsAbstract && type.IsSealed)
                kind = "static class";
            else if (type.IsAbstract)
                kind = "abstract class";
            else if (type.IsSealed)
                kind = "sealed class";
            else
                kind = "class";

            var header = $"{ObsoletePrefix(type)}public {kind} {FormatType(type)}";
            if (type.IsEnum)
                return header;

            var bases = new List<string>();
            if (type.IsClass && type.BaseType != null && type.BaseType != typeof(object))
                bases.Add(FormatType(type.BaseType));

            var inheritedInterfaces = type.BaseType?.GetInterfaces() ?? Type.EmptyTypes;
            bases.AddRange(type.GetInterfaces().Except(inheritedInterfaces).Select(FormatType).OrderBy(n => n, StringComparer.Ordinal));

            if (bases.Count > 0)
                header += $" : {string.Join(", ", bases)}";

            return header + FormatConstraints(type);
        }

        private static string FormatConstraints(Type type)
        {
            if (!type.IsGenericTypeDefinition)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var parameter in type.GetGenericArguments())
            {
                var constraints = new List<string>();
                var attributes = parameter.GenericParameterAttributes;
                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                    constraints.Add("class");
                if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                    constraints.Add("struct");

                constraints.AddRange(parameter.GetGenericParameterConstraints()
                    .Where(constraint => constraint != typeof(ValueType))
                    .Select(FormatType));

                if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0
                    && (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
                    constraints.Add("new()");

                if (constraints.Count > 0)
                    sb.Append($" where {parameter.Name} : {string.Join(", ", constraints)}");
            }

            return sb.ToString();
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        private static bool TryGetDeclaration(Type type, Dictionary<string, Declaration> declarations, out Declaration declaration)
        {
            // Type lồng nằm cùng file với type chứa nó.
            var outer = type;
            while (outer.IsNested)
                outer = outer.DeclaringType;

            return declarations.TryGetValue(CleanName(type.Name), out declaration)
                   || declarations.TryGetValue(CleanName(outer.Name), out declaration);
        }

        private static string GetFolder(Type type, Dictionary<string, Declaration> declarations)
        {
            if (!TryGetDeclaration(type, declarations, out var declaration))
                return "(không rõ file)";

            var slash = declaration.RelativePath.LastIndexOf('/');
            return slash < 0 ? "(gốc package)" : declaration.RelativePath.Substring(0, slash);
        }

        private static string FormatFileLink(Type type, Dictionary<string, Declaration> declarations, string readRoot)
        {
            return TryGetDeclaration(type, declarations, out var declaration)
                ? $"[{declaration.RelativePath}]({readRoot}/{declaration.RelativePath})"
                : "—";
        }

        private static string FormatNameList(List<string> names)
        {
            if (names.Count == 0)
                return "—";

            var shown = names.Take(MaxOverridableListed).Select(n => $"`{n}`");
            var more = names.Count > MaxOverridableListed ? $" +{names.Count - MaxOverridableListed}" : string.Empty;
            return string.Join(", ", shown) + more;
        }

        private static string StripAccessorPrefix(string accessorName)
        {
            var underscore = accessorName.IndexOf('_');
            return underscore < 0 ? accessorName : accessorName.Substring(underscore + 1);
        }

        private static string CleanName(string name)
        {
            var tick = name.IndexOf('`');
            return tick < 0 ? name : name.Substring(0, tick);
        }

        private static string Access(MethodBase method) => method.IsPublic ? "public" : "protected";

        private static string Access(FieldInfo field) => field.IsPublic ? "public" : "protected";

        private static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

        private static bool IsVisible(FieldInfo field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

        private static bool IsDelegate(Type type) => type.IsSubclassOf(typeof(MulticastDelegate));

        private static bool IsCompilerGenerated(MemberInfo member) =>
            member.Name.IndexOf('<') >= 0 || member.IsDefined(typeof(CompilerGeneratedAttribute), false);

        private static string ObsoletePrefix(MemberInfo member) =>
            member.IsDefined(typeof(ObsoleteAttribute), false) ? "[Obsolete] " : string.Empty;
    }
}
#endif
