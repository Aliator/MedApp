using MedApp.Domain.Calendar;

namespace MedApp.Application.Mdt.Common;

/// <summary>An MDT as its list's chooser shows it: the meeting (with attendees), its specialty's name and its item counts.</summary>
public sealed record MdtMeetingRow(Meeting Meeting, string SpecialtyName, int OpenItemCount, int ItemCount);
