using System.Text.RegularExpressions;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class ChecklistTemplateServices : CoreServices, IChecklistTemplates
    {
        private const int MaxItems = 60;

        private readonly ChecklistTemplatesQueries _templates;

        public ChecklistTemplateServices(DB db, Logger logger, ChecklistTemplatesQueries templates) : base(db, logger)
        {
            _templates = templates;
        }

        public async Task<List<ChecklistTemplateDto>> GetAllAsync()
        {
            var counts = await _templates.CountDvsByTemplateAsync();
            return (await _templates.GetAllWithItemsAsync()).Select(t => ToDto(t, counts.GetValueOrDefault(t.Id))).ToList();
        }

        public async Task<ChecklistTemplateDto?> GetAsync(long id) =>
            await _templates.GetWithItemsAsync(id) is { } t ? ToDto(t, 0) : null;

        public async Task<ServiceResult> SaveAsync(SaveChecklistTemplateRequest r, long byUserId)
        {
            var name = Clean(r.Name);
            // Blank rows are ignored; the order on the form is the order on the cover sheet.
            var items = (r.Items ?? []).Select(Clean).Where(i => i.Length > 0).ToList();

            if (name.Length == 0 || name.Length > 200)
                return ServiceResult.Fail("Please enter the name of the checklist type (up to 200 characters).");
            if (items.Count == 0)
                return ServiceResult.Fail("Please add at least one document to the checklist.");
            if (items.Count > MaxItems)
                return ServiceResult.Fail($"A checklist can have up to {MaxItems} documents.");
            if (items.FirstOrDefault(i => i.Length > 300) is { } tooLong)
                return ServiceResult.Fail($"This document name is too long (up to 300 characters): \"{tooLong[..40]}…\"");
            if (items.GroupBy(i => i, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
                return ServiceResult.Fail($"\"{twice.Key}\" is listed twice.");
            if (await _templates.NameExistsAsync(name, r.Id))
                return ServiceResult.Fail($"There is already a checklist type named \"{name}\".");

            _templates.UserId = byUserId;
            ChecklistTemplates template;
            if (r.Id == 0)
            {
                template = new ChecklistTemplates { Name = name, IsActive = r.IsActive };
                _templates.Add(template);
            }
            else
            {
                var existing = await _templates.GetForEditWithItemsAsync(r.Id);
                if (existing is null) return ServiceResult.Fail("That checklist type no longer exists.");
                template = existing;
                template.Name = name;
                template.IsActive = r.IsActive;
                template.Items.Clear();
            }
            for (var i = 0; i < items.Count; i++)
                template.Items.Add(new ChecklistTemplateItems { SortOrder = i + 1, Label = items[i] });

            await _templates.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} saved checklist type \"{name}\" ({items.Count} items).");
            return ServiceResult.Ok(template.Id);
        }

        public async Task<ServiceResult> DeleteAsync(long id, long byUserId)
        {
            var template = await _templates.SelectByIdAsync(id);
            if (template is null) return ServiceResult.Fail("That checklist type no longer exists.");

            await _templates.DeleteByIdAsync(id);
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} deleted checklist type \"{template.Name}\".");
            return ServiceResult.Ok();
        }

        public async Task<(int Added, int Completed)> RestoreStandardAsync(long byUserId)
        {
            static string Key(string s) => Clean(s).ToUpperInvariant();
            _templates.UserId = byUserId;
            var all = await _templates.GetAllWithItemsAsync();
            int added = 0, completed = 0;
            foreach (var (name, items) in StandardChecklists.All)
            {
                var existing = all.FirstOrDefault(t => Key(t.Name) == Key(name));
                if (existing is null)
                {
                    var template = new ChecklistTemplates { Name = name, IsActive = true };
                    for (var i = 0; i < items.Length; i++)
                        template.Items.Add(new ChecklistTemplateItems { SortOrder = i + 1, Label = items[i] });
                    _templates.Add(template);
                    added++;
                    continue;
                }

                var current = existing.Items.OrderBy(i => i.SortOrder).Select(i => i.Label).ToList();
                var wanted = items.Concat(current.Where(c => !items.Any(s => Key(s) == Key(c)))).ToList();
                if (wanted.Select(Key).SequenceEqual(current.Select(Key))) continue;

                var edit = await _templates.GetForEditWithItemsAsync(existing.Id);
                if (edit is null) continue;
                edit.Items.Clear();
                for (var i = 0; i < wanted.Count; i++)
                    edit.Items.Add(new ChecklistTemplateItems { SortOrder = i + 1, Label = wanted[i] });
                completed++;
            }
            if (added + completed > 0) await _templates.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} restored the standard checklists ({added} added, {completed} completed).");
            return (added, completed);
        }

        private static ChecklistTemplateDto ToDto(ChecklistTemplates t, int dvCount) =>
            new(t.Id, t.Name, t.IsActive, t.Items.OrderBy(i => i.SortOrder).Select(i => i.Label).ToList(), dvCount);

        private static string Clean(string? text) => Regex.Replace((text ?? "").Trim(), @"\s+", " ");
    }
}
