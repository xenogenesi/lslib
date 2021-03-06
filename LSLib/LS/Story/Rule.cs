﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSLib.LS.Story
{
    public class RuleNode : RelNode
    {
        public enum RuleType
        {
            Rule,
            Proc,
            Query
        };

        public List<Call> Calls;
        public List<Variable> Variables;
        public UInt32 Line;
        public UInt32 DerivedGoalId;
        public bool IsQuery;

        public override void Read(OsiReader reader)
        {
            base.Read(reader);
            Calls = reader.ReadList<Call>();

            Variables = new List<Variable>();
            var variables = reader.ReadByte();
            while (variables-- > 0)
            {
                var type = reader.ReadByte();
                if (type != 1) throw new InvalidDataException("Illegal value type in rule variable list");
                var variable = new Variable();
                variable.Read(reader);
                if (variable.Adapted)
                {
                    variable.VariableName = String.Format("_Var{0}", Variables.Count + 1);
                }

                Variables.Add(variable);
            }

            Line = reader.ReadUInt32();

            if (reader.MajorVersion > 1 || (reader.MajorVersion == 1 && reader.MinorVersion >= 6))
                IsQuery = reader.ReadBoolean();
            else
                IsQuery = false;
        }

        public override void Write(OsiWriter writer)
        {
            base.Write(writer);
            writer.WriteList<Call>(Calls);

            writer.Write((byte)Variables.Count);
            foreach (var variable in Variables)
            {
                writer.Write((byte)1);
                variable.Write(writer);
            }

            writer.Write(Line);
            if (writer.MajorVersion > 1 || (writer.MajorVersion == 1 && writer.MinorVersion >= 6))
                writer.Write(IsQuery);
        }

        public override Type NodeType()
        {
            return Type.Rule;
        }

        public override string TypeName()
        {
            if (IsQuery)
                return "Query Rule";
            else
                return "Rule";
        }

        public override void DebugDump(TextWriter writer, Story story)
        {
            base.DebugDump(writer, story);

            writer.WriteLine("    Variables: ");
            foreach (var v in Variables)
            {
                writer.Write("        ");
                v.DebugDump(writer, story);
                writer.WriteLine("");
            }

            writer.WriteLine("    Calls: ");
            foreach (var call in Calls)
            {
                writer.Write("        ");
                call.DebugDump(writer, story);
                writer.WriteLine("");
            }
        }

        public Node GetRoot(Story story)
        {
            Node parent = this;
            for (;;)
            {
                if (parent is RelNode)
                {
                    var rel = parent as RelNode;
                    parent = story.Nodes[rel.ParentRef.NodeIndex];
                }
                else if (parent is JoinNode)
                {
                    var join = parent as JoinNode;
                    parent = story.Nodes[join.LeftParentRef.NodeIndex];
                }
                else
                {
                    return parent;
                }
            }
        }

        public RuleType GetRuleType(Story story)
        {
            var root = GetRoot(story);
            if (root is ProcNode)
            {
                if (IsQuery)
                    return RuleType.Query;
                else
                    return RuleType.Proc;
            }
            else
                return RuleType.Rule;
        }

        public Tuple MakeInitialTuple()
        {
            var tuple = new Tuple();
            for (int i = 0; i < Variables.Count; i++)
            {
                tuple.Physical.Add(Variables[i]);
                tuple.Logical.Add(i, Variables[i]);
            }

            return tuple;
        }

        public override void MakeScript(TextWriter writer, Story story, Tuple tuple)
        {
            switch (GetRuleType(story))
            {
                case RuleType.Proc: writer.WriteLine("PROC"); break;
                case RuleType.Query: writer.WriteLine("QRY"); break;
                case RuleType.Rule: writer.WriteLine("IF"); break;
            }

            var initialTuple = MakeInitialTuple();
            story.Nodes[ParentRef.NodeIndex].MakeScript(writer, story, initialTuple);
            writer.WriteLine("THEN");
            foreach (var call in Calls)
            {
                call.MakeScript(writer, story, initialTuple);
                writer.WriteLine();
            }
        }
    }
}
