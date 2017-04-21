using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;


namespace XtraTools.Tasks
{

	public static class StronglyTypedConfigBuilder
	{

		public static CodeCompileUnit Create(IEnumerable<KeyValuePair<string, string>> settings, string baseName, string generatedCodeNamespace, CodeDomProvider codeProvider, bool internalClass)
		{
			generatedCodeNamespace = codeProvider.CreateEscapedIdentifier(generatedCodeNamespace);
			baseName = codeProvider.CreateEscapedIdentifier(baseName);

			var kTypeParam = new CodeTypeParameter("TKey");
			var vTypeParam = new CodeTypeParameter("TValue");
			var tTypeParam = new CodeTypeParameter("T");

			var tType = new CodeTypeReference(tTypeParam);
			var objectType = new CodeTypeReference(typeof(Object));
			var stringType = new CodeTypeReference(typeof(String));
			var propertyInfoType = new CodeTypeReference(typeof(PropertyInfo));
			var stringIndexerType = new CodeTypeReference("IIndexer", stringType, stringType);
			var configIndexerType = new CodeTypeReference("ConfigIndexer", new CodeTypeReference(new CodeTypeParameter(baseName)));
			var configGenericIndexerType = new CodeTypeReference("ConfigIndexer", new CodeTypeReference(tTypeParam));

			var bindingFlags = new CodeTypeReferenceExpression(typeof(BindingFlags));
			var configManager = new CodeTypeReferenceExpression(typeof(ConfigurationManager));
			var thisProperties = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "_properties");
			var index = new CodeVariableReferenceExpression("index");
			var props = new CodeVariableReferenceExpression("props");
			var prop = new CodeVariableReferenceExpression("prop");
			var result = new CodeVariableReferenceExpression("result");
			var key = new CodeArgumentReferenceExpression("key");

			var indexerInterface = new CodeTypeDeclaration("IIndexer") {
				TypeAttributes = TypeAttributes.Public | TypeAttributes.Interface,
				TypeParameters = { kTypeParam, vTypeParam },
				Members = {
					new CodeMemberProperty {
						Name = "Item",
						Type = new CodeTypeReference(vTypeParam),
						HasGet = true,
						Parameters = {new CodeParameterDeclarationExpression(new CodeTypeReference(kTypeParam), "key")}
					}
				}
			};

			var configIndexerConstructor = new CodeConstructor {
				Attributes = MemberAttributes.Assembly,
				Statements = {
					new CodeVariableDeclarationStatement(
						typeof(PropertyInfo[]),
						"props",
						new CodeMethodInvokeExpression(
							new CodeTypeOfExpression(tType),
							"GetProperties",
							new CodeBinaryOperatorExpression(
								new CodeFieldReferenceExpression(bindingFlags, "Public"),
								CodeBinaryOperatorType.BitwiseOr,
								new CodeFieldReferenceExpression(bindingFlags, "Static")
							)
						)
					),
					new CodeIterationStatement(
						new CodeVariableDeclarationStatement(typeof(int), "index", new CodePrimitiveExpression(0)),
						new CodeBinaryOperatorExpression(
							index,
							CodeBinaryOperatorType.LessThan,
							new CodeFieldReferenceExpression(props, "Length")
						),
						new CodeAssignStatement(
							index,
							new CodeBinaryOperatorExpression(index, CodeBinaryOperatorType.Add, new CodePrimitiveExpression(1))
						)
					) {
						Statements = {
							new CodeVariableDeclarationStatement(typeof(PropertyInfo), "prop", new CodeArrayIndexerExpression(props, index)),
							new CodeMethodInvokeExpression(thisProperties, "Add", new CodePropertyReferenceExpression(prop, "Name"), prop)
						}
					}
				}
			};

			var normalizePropertyName = new CodeMemberMethod {
				Attributes = MemberAttributes.Static | MemberAttributes.Private,
				Name = "NormalizePropertyName",
				ReturnType = stringType,
				Parameters = { new CodeParameterDeclarationExpression(stringType, "key") },
				Statements = {
					new CodeConditionStatement(
						new CodeMethodInvokeExpression(
							new CodeTypeReferenceExpression(typeof(String)),
							"IsNullOrEmpty",
							key
						),
						new CodeMethodReturnStatement(key)
					),
					new CodeVariableDeclarationStatement(
						typeof(StringBuilder),
						"sb",
						new CodeObjectCreateExpression(typeof(StringBuilder))
					),
					new CodeIterationStatement(
						new CodeVariableDeclarationStatement(typeof(int), index.VariableName, new CodePrimitiveExpression(0)),
						new CodeBinaryOperatorExpression(
							index,
							CodeBinaryOperatorType.LessThan,
							new CodePropertyReferenceExpression(key, "Length")
						),
						new CodeAssignStatement(
							index,
							new CodeBinaryOperatorExpression(
								index,
								CodeBinaryOperatorType.Add,
								new CodePrimitiveExpression(1)
							)
						),
						new CodeVariableDeclarationStatement(
							typeof(char),
							"c",
							new CodeArrayIndexerExpression(key, index)
						),
						new CodeConditionStatement(
							new CodeBinaryOperatorExpression(
								new CodePropertyReferenceExpression(new CodeVariableReferenceExpression("sb"), "Length"),
								CodeBinaryOperatorType.ValueEquality,
								new CodePrimitiveExpression(0)
							),
							trueStatements: new CodeStatement[] {
								new CodeConditionStatement(
									new CodeBinaryOperatorExpression(
										new CodeMethodInvokeExpression(
											new CodeTypeReferenceExpression(typeof(Char)),
											"IsLetter",
											new CodeVariableReferenceExpression("c")
										),
										CodeBinaryOperatorType.BooleanOr,
										new CodeBinaryOperatorExpression(
											new CodeVariableReferenceExpression("c"),
											CodeBinaryOperatorType.ValueEquality,
											new CodePrimitiveExpression('_')
										)
									),
									new CodeExpressionStatement(
										new CodeMethodInvokeExpression(
											new CodeVariableReferenceExpression("sb"),
											"Append",
											new CodeVariableReferenceExpression("c")
										)
									)
								)
							},
							falseStatements: new CodeStatement[] {
								new CodeConditionStatement(
									new CodeBinaryOperatorExpression(
										new CodeMethodInvokeExpression(
											new CodeTypeReferenceExpression(typeof(Char)),
											"IsLetter",
											new CodeVariableReferenceExpression("c")
										),
										CodeBinaryOperatorType.BooleanOr,
										new CodeBinaryOperatorExpression(
											new CodeMethodInvokeExpression(
												new CodeTypeReferenceExpression(typeof(Char)),
												"IsNumber",
												new CodeVariableReferenceExpression("c")
											),
											CodeBinaryOperatorType.BooleanOr,
											new CodeBinaryOperatorExpression(
												new CodeVariableReferenceExpression("c"),
												CodeBinaryOperatorType.ValueEquality,
												new CodePrimitiveExpression('_')
											)
										)
									),
									new CodeExpressionStatement(
										new CodeMethodInvokeExpression(
											new CodeVariableReferenceExpression("sb"),
											"Append",
											new CodeVariableReferenceExpression("c")
										)
									)
								)
							}
						)
					),
					new CodeMethodReturnStatement(
						new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("sb"), "ToString")
					)
				}
			};

			var configIndexerClass = new CodeTypeDeclaration("ConfigIndexer") {
				TypeAttributes = TypeAttributes.NestedPrivate,
				TypeParameters = { tTypeParam },
				BaseTypes = { objectType, stringIndexerType },
				Members = {
					configIndexerConstructor,
					normalizePropertyName,
					new CodeMemberField(typeof(IDictionary<string, PropertyInfo>), "_properties") {
						Attributes = MemberAttributes.Private,
						InitExpression = new CodeObjectCreateExpression(typeof(Dictionary<string, PropertyInfo>))
					},
					new CodeMemberProperty {
						Attributes = MemberAttributes.Public | MemberAttributes.Final,
						ImplementationTypes = { stringIndexerType },
						Name = "Item",
						Type = stringType,
						Parameters = {new CodeParameterDeclarationExpression(stringType, "key")},
						GetStatements = {
							new CodeVariableDeclarationStatement(
								stringType,
								result.VariableName,
								new CodeIndexerExpression(
									new CodePropertyReferenceExpression(configManager, "AppSettings"),
									new CodeArgumentReferenceExpression("key")
								)
							),
							new CodeConditionStatement(
								new CodeBinaryOperatorExpression(
									result,
									CodeBinaryOperatorType.IdentityInequality,
									new CodePrimitiveExpression(null)
								),
								new CodeMethodReturnStatement(result)
							),
							new CodeVariableDeclarationStatement(propertyInfoType, "prop"),
							new CodeConditionStatement(
								new CodeMethodInvokeExpression(
									thisProperties,
									"TryGetValue",
									new CodeMethodInvokeExpression(
										new CodeTypeReferenceExpression(configGenericIndexerType),
										normalizePropertyName.Name,
										key
									),
									new CodeDirectionExpression(FieldDirection.Out, prop)
								),
								new CodeMethodReturnStatement(
									new CodeMethodInvokeExpression(
										new CodeMethodInvokeExpression(prop, "GetValue", new CodeTypeOfExpression(tType)),
										"ToString"
									)
								)
							),
							new CodeMethodReturnStatement(new CodePrimitiveExpression(null))
						}
					}
				}
			};

			var members = settings
				.Select(
					setting => new CodeMemberProperty {
						Attributes = MemberAttributes.Public | MemberAttributes.Static,
						Type = stringType,
						Name = VerifyConfigKey(setting.Key, codeProvider),
						GetStatements = {
							new CodeVariableDeclarationStatement(
								stringType,
								result.VariableName,
								new CodeIndexerExpression(
									new CodePropertyReferenceExpression(configManager, "AppSettings"),
									new CodePrimitiveExpression(setting.Key)
								)
							),
							new CodeConditionStatement(
								new CodeBinaryOperatorExpression(
									new CodeVariableReferenceExpression(result.VariableName),
									CodeBinaryOperatorType.IdentityInequality,
									new CodePrimitiveExpression(null)
								),
								new CodeMethodReturnStatement(result)
							),
							new CodeMethodReturnStatement(new CodePrimitiveExpression(setting.Value))
						}
					}
				)
				.Cast<CodeTypeMember>()
				.ToArray();

			var configClass = new CodeTypeDeclaration(baseName) {
				TypeAttributes = (internalClass ? TypeAttributes.NestedAssembly : TypeAttributes.Public) | TypeAttributes.Sealed,
				CustomAttributes = {
					new CodeAttributeDeclaration(
						new CodeTypeReference(typeof(GeneratedCodeAttribute), CodeTypeReferenceOptions.GlobalReference),
						new CodeAttributeArgument(new CodePrimitiveExpression(StronglyTypedConfigBuilderType.FullName)),
						new CodeAttributeArgument(new CodePrimitiveExpression(StronglyTypedConfigBuilderType.Assembly.GetName().Version.ToString()))
					),
					new CodeAttributeDeclaration(
						new CodeTypeReference(typeof(DebuggerNonUserCodeAttribute), CodeTypeReferenceOptions.GlobalReference)
					),
					new CodeAttributeDeclaration(
						new CodeTypeReference(typeof(CompilerGeneratedAttribute), CodeTypeReferenceOptions.GlobalReference)
					)
				},
				Members = {
					new CodeConstructor { Attributes = MemberAttributes.Private },
					new CodeMemberField {
						Attributes = MemberAttributes.Private | MemberAttributes.Static,
						Name = "_items",
						Type = stringIndexerType,
						InitExpression = new CodeObjectCreateExpression(configIndexerType)
					},
					new CodeMemberProperty {
						Attributes = MemberAttributes.Public | MemberAttributes.Static,
						Name = "Items",
						Type = stringIndexerType,
						GetStatements = {
							new CodeMethodReturnStatement(new CodeFieldReferenceExpression(null, "_items"))
						}
					},
					indexerInterface,
					configIndexerClass,
				}
			};
			configClass.Members.AddRange(members);

			var configNamespace = new CodeNamespace(generatedCodeNamespace);
			configNamespace.Types.Add(configClass);

			var compileUnit = new CodeCompileUnit();
			compileUnit.Namespaces.Add(configNamespace);

			return compileUnit;
		}


		public static string VerifyConfigKey(string key, CodeDomProvider provider)
		{
			if (String.IsNullOrEmpty(key)) {
				return key;
			}
			var sb = new StringBuilder();
			foreach (char c in key) {
				if (sb.Length == 0) {
					if (Char.IsLetter(c) || c == '_') {
						sb.Append(c);
					}
				} else {
					if (Char.IsLetter(c) || Char.IsNumber(c) || c == '_') {
						sb.Append(c);
					}
				}
			}
			return provider.CreateEscapedIdentifier(sb.ToString());
		}


		private static readonly Type StronglyTypedConfigBuilderType = typeof(StronglyTypedConfigBuilder);

	}

}
