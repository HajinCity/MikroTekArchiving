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

        private static ChecklistTemplateDto ToDto(ChecklistTemplates t, int dvCount) =>
            new(t.Id, t.Name, t.IsActive, t.Items.OrderBy(i => i.SortOrder).Select(i => i.Label).ToList(), dvCount);

        private static string Clean(string? text) => Regex.Replace((text ?? "").Trim(), @"\s+", " ");
    }
}
