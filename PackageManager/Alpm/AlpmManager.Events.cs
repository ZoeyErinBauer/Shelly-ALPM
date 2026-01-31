using System;
using System.Runtime.InteropServices;

namespace PackageManager.Alpm;

public partial class AlpmManager
{
    private void HandleQuestion(IntPtr ctx, IntPtr questionPtr)
    {
        var question = Marshal.PtrToStructure<AlpmQuestionAny>(questionPtr);
        var questionType = (AlpmQuestionType)question.Type;

        var questionText = questionType switch
        {
            AlpmQuestionType.InstallIgnorePkg => "Install IgnorePkg?",
            AlpmQuestionType.ReplacePkg => "Replace package?",
            AlpmQuestionType.ConflictPkg => "Conflict found. Remove?",
            AlpmQuestionType.CorruptedPkg => "Corrupted pkg. Delete?",
            AlpmQuestionType.ImportKey => "Import GPG key?",
            AlpmQuestionType.SelectProvider => "Select provider?",
            _ => $"Unknown question type: {question.Type}"
        };

        var args = new AlpmQuestionEventArgs(questionType, questionText);
        Question?.Invoke(this, args);

        Console.Error.WriteLine($"[ALPM_QUESTION] {questionText} (Answering {args.Response})");

        // Write the response back to the answer field.
        question.Answer = args.Response;
        Marshal.StructureToPtr(question, questionPtr, false);
    }

    private void HandleProgress(IntPtr ctx, AlpmProgressType progress, IntPtr pkgNamePtr, int percent, ulong howmany,
        ulong current)
    {
        try
        {
            string? pkgName = pkgNamePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(pkgNamePtr) : null;
            Console.Error.WriteLine($"[DEBUG_LOG] ALPM Progress: {progress}, Pkg: {pkgName}, %: {percent}");

            Progress?.Invoke(this, new AlpmProgressEventArgs(
                progress,
                pkgName,
                percent,
                howmany,
                current
            ));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ALPM_ERROR] Error in progress callback: {ex.Message}");
        }
    }

    private void HandleEvent(IntPtr ctx, IntPtr eventPtr)
    {
        // Early return for null pointer
        if (eventPtr == IntPtr.Zero) return;

        // Additional safety check - if handle is disposed, don't process events
        if (_handle == IntPtr.Zero) return;

        int typeValue;
        try
        {
            // Read the type field directly using ReadInt32
            typeValue = Marshal.ReadInt32(eventPtr);
        }
        catch (AccessViolationException)
        {
            // Memory access violation - pointer is invalid, silently ignore
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ALPM_ERROR] Error reading event type: {ex.Message}");
            return;
        }

        // Validate the type value is within expected range (1-37 for ALPM events)
        if (typeValue < 1 || typeValue > 37)
        {
            // Invalid event type - likely corrupted memory or wrong pointer
            return;
        }

        try
        {
            var type = (AlpmEventType)typeValue;

            switch (type)
            {
                case AlpmEventType.CheckDepsStart:
                    Console.Error.WriteLine("[ALPM] Checking dependencies...");
                    break;
                case AlpmEventType.CheckDepsDone:
                    Console.Error.WriteLine("[ALPM] Dependency check finished.");
                    break;
                case AlpmEventType.FileConflictsStart:
                    Console.Error.WriteLine("[ALPM] Checking for file conflicts...");
                    break;
                case AlpmEventType.FileConflictsDone:
                    Console.Error.WriteLine("[ALPM] File conflict check finished.");
                    break;
                case AlpmEventType.ResolveDepsStart:
                    Console.Error.WriteLine("[ALPM] Resolving dependencies...");
                    break;
                case AlpmEventType.ResolveDepsDone:
                    Console.Error.WriteLine("[ALPM] Dependency resolution finished.");
                    break;
                case AlpmEventType.InterConflictsStart:
                    Console.Error.WriteLine("[ALPM] Checking for inter-conflicts...");
                    break;
                case AlpmEventType.InterConflictsDone:
                    Console.Error.WriteLine("[ALPM] Inter-conflict check finished.");
                    break;
                case AlpmEventType.TransactionStart:
                    Console.Error.WriteLine("[ALPM] Starting transaction...");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, null));
                    break;
                case AlpmEventType.TransactionDone:
                    Console.Error.WriteLine("[ALPM] Transaction successfully finished.");
                    PackageOperation?.Invoke(this, new AlpmPackageOperationEventArgs(type, null));
                    break;
                case AlpmEventType.IntegrityStart:
                    Console.Error.WriteLine("[ALPM] Checking package integrity...");
                    break;
                case AlpmEventType.IntegrityDone:
                    Console.Error.WriteLine("[ALPM] Integrity check finished.");
                    break;
                case AlpmEventType.LoadStart:
                    Console.Error.WriteLine("[ALPM] Loading packages...");
                    break;
                case AlpmEventType.LoadDone:
                    Console.Error.WriteLine("[ALPM] Packages loaded.");
                    break;
                case AlpmEventType.DiskspaceStart:
                    Console.Error.WriteLine("[ALPM] Checking available disk space...");
                    break;
                case AlpmEventType.DiskspaceDone:
                    Console.Error.WriteLine("[ALPM] Disk space check finished.");
                    break;

                case AlpmEventType.PackageOperationStart:
                {
                    Console.Error.WriteLine("[ALPM] Starting package operation...");
                    break;
                }

                case AlpmEventType.PackageOperationDone:
                {
                    Console.Error.WriteLine("[ALPM] Package operation finished.");
                    break;
                }

                case AlpmEventType.ScriptletInfo:
                {
                    Console.Error.WriteLine("[ALPM] Running scriptlet...");
                    break;
                }

                case AlpmEventType.HookStart:
                    Console.Error.WriteLine("[ALPM] Running hooks...");
                    break;
                case AlpmEventType.HookDone:
                    Console.Error.WriteLine("[ALPM] Hooks finished.");
                    break;

                case AlpmEventType.HookRunStart:
                {
                    Console.Error.WriteLine("[ALPM] Running hook...");
                    break;
                }
                case AlpmEventType.HookRunDone:
                    Console.Error.WriteLine("[ALPM] Hook finished.");
                    break;

                // Database retrieval events (for sync operations)
                case AlpmEventType.DbRetrieveStart:
                    Console.Error.WriteLine("[ALPM] Retrieving database...");
                    break;
                case AlpmEventType.DbRetrieveDone:
                    Console.Error.WriteLine("[ALPM] Database retrieved.");
                    break;
                case AlpmEventType.DbRetrieveFailed:
                    Console.Error.WriteLine("[ALPM] Database retrieval failed.");
                    break;

                // Package retrieval events
                case AlpmEventType.PkgRetrieveStart:
                    Console.Error.WriteLine("[ALPM] Retrieving packages...");
                    break;
                case AlpmEventType.PkgRetrieveDone:
                    Console.Error.WriteLine("[ALPM] Packages retrieved.");
                    break;
                case AlpmEventType.PkgRetrieveFailed:
                    Console.Error.WriteLine("[ALPM] Package retrieval failed.");
                    break;

                case AlpmEventType.DatabaseMissing:
                {
                    Console.Error.WriteLine(
                        "[ALPM] Database missing. Please run 'pacman-key --init' to initialize it.");
                    break;
                }

                case AlpmEventType.OptdepRemoval:
                    Console.Error.WriteLine("[ALPM] Optional dependency being removed.");
                    break;

                case AlpmEventType.KeyringStart:
                    Console.Error.WriteLine("[ALPM] Checking keyring...");
                    break;
                case AlpmEventType.KeyringDone:
                    Console.Error.WriteLine("[ALPM] Keyring check finished.");
                    break;
                case AlpmEventType.KeyDownloadStart:
                    Console.Error.WriteLine("[ALPM] Downloading keys...");
                    break;
                case AlpmEventType.KeyDownloadDone:
                    Console.Error.WriteLine("[ALPM] Key download finished.");
                    break;

                case AlpmEventType.PacnewCreated:
                    Console.Error.WriteLine("[ALPM] .pacnew file created.");
                    break;
                case AlpmEventType.PacsaveCreated:
                    Console.Error.WriteLine("[ALPM] .pacsave file created.");
                    break;

                default:
                    // Unknown event type - just ignore it
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ALPM_ERROR] Error handling event: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely reads a string pointer from an event struct at the given offset.
    /// Returns null if the pointer is invalid or reading fails.
    /// </summary>
    private static string? ReadStringFromEvent(IntPtr eventPtr, int offset)
    {
        try
        {
            IntPtr strPtr = Marshal.ReadIntPtr(eventPtr, offset);
            if (strPtr == IntPtr.Zero) return null;
            return Marshal.PtrToStringUTF8(strPtr);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely reads the package name from a PackageOperation event.
    /// The struct layout is: type (4) + operation (4) + oldpkg ptr + newpkg ptr
    /// </summary>
    private string? ReadPackageNameFromEvent(IntPtr eventPtr)
    {
        try
        {
            const int ptrOffset = 8; // type (4) + operation (4)
            IntPtr oldPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset);
            IntPtr newPkgPtr = Marshal.ReadIntPtr(eventPtr, ptrOffset + IntPtr.Size);

            // For install/upgrade, use NewPkgPtr; for remove, use OldPkgPtr
            IntPtr pkgPtr = newPkgPtr != IntPtr.Zero ? newPkgPtr : oldPkgPtr;
            if (pkgPtr == IntPtr.Zero) return null;

            IntPtr namePtr = GetPkgName(pkgPtr);
            if (namePtr == IntPtr.Zero) return null;

            return Marshal.PtrToStringUTF8(namePtr);
        }
        catch
        {
            return null;
        }
    }
}
