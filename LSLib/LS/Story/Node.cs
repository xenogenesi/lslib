﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSLib.LS.Story
{
    abstract public class Node : OsirisSerializable
    {
        public enum Type : byte
        {
            Database = 1,
            Proc = 2,
            DivQuery = 3,
            And = 4,
            NotAnd = 5,
            RelOp = 6,
            Rule = 7,
            InternalQuery = 8,
            UserQuery = 9
        };

        public DatabaseRef DatabaseRef;
        public string Name;
        public byte NameIndex;

        public virtual void Read(OsiReader reader)
        {
            DatabaseRef = reader.ReadDatabaseRef();
            Name = reader.ReadString();
            if (Name.Length > 0)
            {
                NameIndex = reader.ReadByte();
            }
        }

        public virtual void Write(OsiWriter writer)
        {
            DatabaseRef.Write(writer);
            writer.Write(Name);
            if (Name.Length > 0)
                writer.Write(NameIndex);
        }

        abstract public Type NodeType();

        abstract public string TypeName();

        abstract public void MakeScript(TextWriter writer, Story story, Tuple tuple);

        public virtual void DebugDump(TextWriter writer, Story story)
        {
            if (Name.Length > 0)
            {
                writer.Write("{0}/{1}: ", Name, NameIndex);
            }

            writer.Write("<{0}>", TypeName());
            if (DatabaseRef.IsValid())
            {
                writer.Write(", Database ");
                DatabaseRef.DebugDump(writer, story);
            }

            writer.WriteLine();
        }
    }


    abstract public class TreeNode : Node
    {
        public NodeEntryItem NextNode;

        public override void Read(OsiReader reader)
        {
            base.Read(reader);
            NextNode = new NodeEntryItem();
            NextNode.Read(reader);
        }

        public override void Write(OsiWriter writer)
        {
            base.Write(writer);
            NextNode.Write(writer);
        }

        public override void DebugDump(TextWriter writer, Story story)
        {
            base.DebugDump(writer, story);

            writer.Write("    Next: ");
            NextNode.DebugDump(writer, story);
            writer.WriteLine("");
        }
    }
}
