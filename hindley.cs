namespace HindleyMilner
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public interface INode
    {
        TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric);
    }

    public class TypeNode
    {
        public string Name { get; set; }
        public List<TypeNode> ArgumentTypes { get; set; }
        public TypeNode ResultType { get; set; }

        public TypeNode FreshCopy(Dictionary<TypeNode, TypeNode> Mappings, List<TypeNode> NonGeneric)
        {
            if (Name == null)
            {
                if (OccursIn(NonGeneric))
                {
                    return this;
                }
                else
                {
                    if (!Mappings.ContainsKey(this))
                    {
                        Mappings[this] = new TypeNode();
                    }
                    return Mappings[this];
                }
            }
            else
            {
                return new TypeNode()
                {
                    Name = Name,
                    ResultType = ResultType == null ? null : ResultType.FreshCopy(Mappings, NonGeneric),
                    ArgumentTypes = ArgumentTypes == null ? new List<TypeNode>() : ArgumentTypes.Select(a => a.FreshCopy(Mappings, NonGeneric)).ToList()
                };
            }
        }

        public bool OccursInType(TypeNode Node2)
        {
            return this == Node2 || (Node2.Name != null && Node2.ArgumentTypes != null && OccursIn(Node2.ArgumentTypes));
        }

        public bool OccursIn(List<TypeNode> Types)
        {
            return Types.Any(a => this.OccursInType(a));
        }

        public void Unify(TypeNode Other)
        {
            if (this.Name == null)
            {
                if (this == Other)
                {
                    throw new Exception("Attempt to unify a type with itself.");
                }
                if (OccursInType(Other))
                {
                    throw new Exception("Recursive Unification");
                }

                ResultType = Other.ResultType;
                Name = Other.Name;

                ArgumentTypes = new List<TypeNode>();
                if (Other.ArgumentTypes != null)
                    foreach (var item in Other.ArgumentTypes)
                        ArgumentTypes.Add(item);
            }
            else
            {
                if (Other.Name == null)
                {
                    Other.Unify(this);
                }
                else
                {
                    if (this.Name != (Other as TypeNode).Name || ArgumentTypes.Count != (Other as TypeNode).ArgumentTypes.Count || (this.ResultType == null) != ((Other as TypeNode).ResultType == null))
                    {
                        throw new Exception("Type mismatch!");
                    }

                    for (int i = 0; i < ArgumentTypes.Count; i++)
                    {
                        ArgumentTypes[i].Unify(Other.ArgumentTypes[i]);
                    }

                    if (Other.ResultType != null)
                    {
                        Other.ResultType.Unify(this.ResultType);
                    }
                }
            }
        }
    }

    public class IntNode : INode
    {
        public Int32 Value { get; set; }

        public TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric)
        {
            return new TypeNode() { Name = "Integer" };
        }
    }

    public class IdentityNode : INode
    {
        public string Value { get; set; }

        public TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric)
        {
            if (Environment.ContainsKey(Value) == false)
            {
                throw new Exception(String.Format("Invalid identifier: {0}", Value));
            }

            return Environment[Value].FreshCopy(new Dictionary<TypeNode, TypeNode>(), NonGeneric);
        }
    }

    public class LetNode : INode
    {
        public string Name { get; set; }
        public INode Value { get; set; }
        public INode Body { get; set; }

        public TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric)
        {
            if (Environment.ContainsKey(Name))
            {
                throw new Exception(String.Format("Name {0} is already bound to another value.", Name));
            }

            var NewEnvironment = new Dictionary<string, TypeNode>();
            
            foreach (var item in Environment)
                NewEnvironment[item.Key] = item.Value;

            NewEnvironment[Name] = Value.FindType(Environment, NonGeneric);

            return Body.FindType(NewEnvironment, NonGeneric);
        }
    }

    public class LambdaNode : INode
    {
        public List<string> Arguments { get; set; }
        public INode Body { get; set; }

        public TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric)
        {
            var TemporaryTypes = Arguments.ToDictionary(a => a, a => new TypeNode());

            var NewEnvironment = new Dictionary<string, TypeNode>();
            foreach (var item in Environment)
                NewEnvironment[item.Key] = item.Value;
            foreach (var type in TemporaryTypes)
                NewEnvironment[type.Key] = type.Value;

            var NewNonGeneric = new List<TypeNode>();
            foreach (var item in NonGeneric)
                NewNonGeneric.Add(item);
            foreach (var type in TemporaryTypes)
                NewNonGeneric.Add(type.Value);

            var ResultType = Body.FindType(NewEnvironment, NewNonGeneric);
            return new TypeNode()
            {
                Name = "lambda",
                ArgumentTypes = TemporaryTypes.Select(a => (TypeNode)a.Value).ToList(),
                ResultType = ResultType
            };
        }
    }

    public class ApplyNode : INode
    {
        public INode Name { get; set; }
        public List<INode> Arguments { get; set; }

        public TypeNode FindType(Dictionary<string, TypeNode> Environment, List<TypeNode> NonGeneric)
        {
            var ResultType = new TypeNode();
            var ArgumentTypesB = Arguments.Select(a => a.FindType(Environment, NonGeneric)).ToList();
            var FunctionType = Name.FindType(Environment, NonGeneric);
            new TypeNode()
            {
                Name = "lambda",
                ArgumentTypes = ArgumentTypesB,
                ResultType = ResultType
            }.Unify(FunctionType);
            return ResultType;
        }
    }

    public class HindleyMilner
    {
        public static void Main()
        {
            var programs = new List<INode> {
                new LambdaNode() { Arguments = new[] { "f" }.ToList(), Body = new IntNode() { Value = 5 }},

                new LetNode()
                {
                    Name = "five",
                    Value = new IntNode()
                    {
                        Value = 5
                    },
                    Body = new LetNode()
                    {
                        Name = "g",
                        Value = new LambdaNode()
                        {
                            Arguments = new[] { "f" }.ToList(),
                            Body = new IdentityNode()
                            {
                                Value = "f"
                            }
                        },
                        Body = new ApplyNode()
                        {
                            Name = new IdentityNode()
                            {
                                Value = "g"
                            },
                            Arguments = new[]
                            {
                                (INode)new IdentityNode()
                                {
                                    Value = "five"
                                }
                            }.ToList()
                        }
                    }
                }
            };

            foreach (var program in programs)
            {
                var Environment = new Dictionary<string, TypeNode>();
                var NonGeneric = new List<TypeNode>();

                var FoundType = program.FindType(Environment, NonGeneric);

                Console.WriteLine("Current Program: ");
                Console.WriteLine(JsonConvert.SerializeObject(program, Formatting.Indented));
                Console.WriteLine("Result: ");
                Console.WriteLine(JsonConvert.SerializeObject(FoundType, Formatting.Indented));
            }

            Console.ReadKey();
        }
    }
}
