﻿using zlib;
using LZ4;
using LSLib.Granny;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LSLib.LS
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LSPKHeader7
    {
        public UInt32 Version;
        public UInt32 DataOffset;
        public UInt32 NumParts;
        public UInt32 FileListSize;
        public Byte LittleEndian;
        public UInt32 NumFiles;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FileEntry7
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Name;
        public UInt32 OffsetInFile;
        public UInt32 SizeOnDisk;
        public UInt32 UncompressedSize;
        public UInt32 ArchivePart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LSPKHeader10
    {
        public UInt32 Version;
        public UInt32 DataOffset;
        public UInt32 FileListSize;
        public UInt16 NumParts;
        public UInt16 SomePartVar;
        public UInt32 NumFiles;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LSPKHeader13
    {
        public UInt32 Version;
        public UInt32 FileListOffset;
        public UInt32 FileListSize;
        public UInt16 NumParts;
        public UInt16 SomePartVar;
        public Guid ArchiveGuid;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct FileEntry13
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Name;
        public UInt32 OffsetInFile;
        public UInt32 SizeOnDisk;
        public UInt32 UncompressedSize;
        public UInt32 ArchivePart;
        public UInt32 Flags;
        public UInt32 Crc;
    }

    abstract public class FileInfo
    {
        public String Name;

        abstract public UInt32 Size();
        abstract public UInt32 CRC();
        abstract public BinaryReader MakeReader();
    }

    public class PackagedFileInfo : FileInfo
    {
        public Stream PackageStream;
        public UInt32 OffsetInFile;
        public UInt32 SizeOnDisk;
        public UInt32 UncompressedSize;
        public UInt32 ArchivePart;
        public UInt32 Flags;
        public UInt32 Crc;

        public override UInt32 Size()
        {
            if ((Flags & 0x0F) == 0)
                return SizeOnDisk;
            else
                return UncompressedSize;
        }

        public override UInt32 CRC()
        {
            return Crc;
        }

        public override BinaryReader MakeReader()
        {
            var compressed = new byte[SizeOnDisk];

            this.PackageStream.Seek(OffsetInFile, SeekOrigin.Begin);
            int readSize = this.PackageStream.Read(compressed, 0, (int)SizeOnDisk);
            if (readSize != SizeOnDisk)
            {
                var msg = String.Format("Failed to read {0} bytes from archive (only got {1})", SizeOnDisk, readSize);
                throw new InvalidDataException(msg);
            }

            if (Crc != 0)
            {
                var computedCrc = Crc32.Compute(compressed);
                if (computedCrc != Crc)
                {
                    var msg = String.Format(
                        "CRC check failed on file '{0}', archive is possibly corrupted. Expected {1,8:X}, got {2,8:X}",
                        Name, Crc, computedCrc
                    );
                    throw new InvalidDataException(msg);
                }
            }

            var uncompressed = BinUtils.Decompress(compressed, (int)Size(), (byte)Flags);
            var memStream = new MemoryStream(uncompressed);
            var reader = new BinaryReader(memStream);
            return reader;
        }

        internal static PackagedFileInfo CreateFromEntry(FileEntry13 entry, Stream dataStream)
        {
            var info = new PackagedFileInfo();
            info.PackageStream = dataStream;

            var nameLen = 0;
            for (nameLen = 0; nameLen < entry.Name.Length && entry.Name[nameLen] != 0; nameLen++) { }
            info.Name = Encoding.UTF8.GetString(entry.Name, 0, nameLen);

            var compressionMethod = entry.Flags & 0x0F;
            if (compressionMethod > 2 || (entry.Flags & ~0x7F) != 0)
            {
                var msg = String.Format("File '{0}' has unsupported flags: {1}", info.Name, entry.Flags);
                throw new InvalidDataException(msg);
            }

            info.OffsetInFile = entry.OffsetInFile;
            info.SizeOnDisk = entry.SizeOnDisk;
            info.UncompressedSize = entry.UncompressedSize;
            info.ArchivePart = entry.ArchivePart;
            info.Flags = entry.Flags;
            info.Crc = entry.Crc;
            return info;
        }

        internal static PackagedFileInfo CreateFromEntry(FileEntry7 entry, Stream dataStream)
        {
            var info = new PackagedFileInfo();
            info.PackageStream = dataStream;

            var nameLen = 0;
            for (nameLen = 0; nameLen < entry.Name.Length && entry.Name[nameLen] != 0; nameLen++) { }
            info.Name = Encoding.UTF8.GetString(entry.Name, 0, nameLen);

            info.OffsetInFile = entry.OffsetInFile;
            info.SizeOnDisk = entry.SizeOnDisk;
            info.UncompressedSize = entry.UncompressedSize;
            info.ArchivePart = entry.ArchivePart;
            info.Crc = 0;

            if (entry.UncompressedSize > 0)
            {
                info.Flags = BinUtils.MakeCompressionFlags(CompressionMethod.Zlib, CompressionLevel.DefaultCompression);
            }
            else
            {
                info.Flags = 0;
            }

            return info;
        }

        internal FileEntry7 MakeEntryV7()
        {
            var entry = new FileEntry7();
            entry.Name = new byte[256];
            var encodedName = Encoding.UTF8.GetBytes(Name.Replace('\\', '/'));
            Array.Copy(encodedName, entry.Name, encodedName.Length);

            entry.OffsetInFile = OffsetInFile;
            entry.SizeOnDisk = SizeOnDisk;
            entry.UncompressedSize = ((Flags & 0x0F) == 0) ? 0 : UncompressedSize;
            entry.ArchivePart = ArchivePart;
            return entry;
        }

        internal FileEntry13 MakeEntryV13()
        {
            var entry = new FileEntry13();
            entry.Name = new byte[256];
            var encodedName = Encoding.UTF8.GetBytes(Name.Replace('\\', '/'));
            Array.Copy(encodedName, entry.Name, encodedName.Length);

            entry.OffsetInFile = OffsetInFile;
            entry.SizeOnDisk = SizeOnDisk;
            entry.UncompressedSize = ((Flags & 0x0F) == 0) ? 0 : UncompressedSize;
            entry.ArchivePart = ArchivePart;
            entry.Flags = Flags;
            entry.Crc = Crc;
            return entry;
        }
    }

    public class FilesystemFileInfo : FileInfo
    {
        public string FilesystemPath;
        public long CachedSize;

        public override UInt32 Size()
        {
            return (UInt32)CachedSize;
        }

        public override UInt32 CRC()
        {
            throw new NotImplementedException("!");
        }

        public override BinaryReader MakeReader()
        {
            var fs = new FileStream(FilesystemPath, FileMode.Open, FileAccess.Read);
            return new BinaryReader(fs);
        }

        public static FilesystemFileInfo CreateFromEntry(string filesystemPath, string name)
        {
            var info = new FilesystemFileInfo();
            info.Name = name;
            info.FilesystemPath = filesystemPath;

            var fsInfo = new System.IO.FileInfo(filesystemPath);
            info.CachedSize = fsInfo.Length;
            return info;
        }
    }

    public class Package
    {
        public static byte[] Signature = new byte[] { 0x4C, 0x53, 0x50, 0x4B };
        public const UInt32 CurrentVersion = 13;

        internal List<FileInfo> Files = new List<FileInfo>();

        public static string MakePartFilename(string path, int part)
        {
            var dirName = Path.GetDirectoryName(path);
            var baseName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            return String.Format("{0}/{1}_{2}{3}", dirName, baseName, part, extension);
        }
    }

    public class Packager
    {
        public delegate void ProgressUpdateDelegate(string status, long numerator, long denominator);
        public ProgressUpdateDelegate progressUpdate = delegate { };

        private void WriteProgressUpdate(FileInfo file, long numerator, long denominator)
        {
            this.progressUpdate(file.Name, numerator, denominator);
        }

        public void UncompressPackage(string packagePath, string outputPath)
        {
            if (outputPath.Length > 0 && outputPath.Last() != '/' && outputPath.Last() != '\\')
                outputPath += "/";

            this.progressUpdate("Reading package headers ...", 0, 1);
            var reader = new PackageReader(packagePath);
            var package = reader.Read();

            long totalSize = package.Files.Sum(p => (long)p.Size());
            long currentSize = 0;

            foreach (var file in package.Files)
            {
                this.progressUpdate(file.Name, currentSize, totalSize);
                currentSize += file.Size();

                var outPath = outputPath + file.Name;
                var dirName = Path.GetDirectoryName(outPath);
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }

                var inReader = file.MakeReader();
                var outFile = new FileStream(outPath, FileMode.Create, FileAccess.Write);

                if (inReader != null)
                {
                    byte[] buffer = new byte[32768];
                    int read;
                    while ((read = inReader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outFile.Write(buffer, 0, read);
                    }

                    inReader.Dispose();
                }

                outFile.Dispose();
            }

            reader.Dispose();
        }

        public void EnumerateFiles(Package package, string rootPath, string currentPath)
        {
            foreach (string filePath in Directory.GetFiles(currentPath))
            {
                var relativePath = filePath.Substring(rootPath.Length);
                if (relativePath[0] == '/' || relativePath[0] == '\\')
                {
                    relativePath = relativePath.Substring(1);
                }

                var fileInfo = FilesystemFileInfo.CreateFromEntry(filePath, relativePath);
                package.Files.Add(fileInfo);
            }

            foreach (string directoryPath in Directory.GetDirectories(currentPath))
            {
                EnumerateFiles(package, rootPath, directoryPath);
            }
        }

        public void CreatePackage(string packagePath, string inputPath, uint version = Package.CurrentVersion, CompressionMethod compression = CompressionMethod.None, bool fastCompression = true)
        {
            this.progressUpdate("Enumerating files ...", 0, 1);
            var package = new Package();
            EnumerateFiles(package, inputPath, inputPath);

            this.progressUpdate("Creating archive ...", 0, 1);
            var writer = new PackageWriter(package, packagePath);
            writer.writeProgress += WriteProgressUpdate;
            writer.Version = version;
            writer.Compression = compression;
            writer.CompressionLevel = fastCompression ? LS.CompressionLevel.FastCompression : LS.CompressionLevel.DefaultCompression;
            writer.Write();
            writer.Dispose();
        }
    }
}
