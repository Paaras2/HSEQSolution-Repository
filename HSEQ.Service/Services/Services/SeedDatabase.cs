using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    public class SeedDatabase : ISeedDatabase
    {
        private readonly ApplicationDbContext _context;

        public SeedDatabase(ApplicationDbContext context)
        {
            _context = context;
        }

        private record ActivitySeed(string Code, string Title);
        private record ManagementSeed(string Code, string Title, ActivitySeed[] Activities);

        // Transcribed from the company's "List of Organizational Management and Activity
        // Type Abbreviations" (QASTL-008-B). Activity codes are only unique within their
        // parent Management (see OrganizationalActivityConfigurations), so "GE" (General)
        // and other short codes intentionally repeat across every Management below.
        private static readonly ManagementSeed[] Data =
        {
            new("D", "مدیریت عامل", new ActivitySeed[]
            {
                new("GE", "عمومی"),
            }),
            new("M", "توسعه بازار و امور بین الملل", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("SP", "برنامه ریزی استراتژیک"),
                new("MI", "توسعه بازار و امور بین الملل"),
                new("MS", "مطالعه بازار و امکان سنجی"),
                new("RD", "تحقیق و توسعه محصول"),
                new("PF", "تامین منابع مالی"),
                new("TE", "مطالعات و پیشنهادات"),
                new("RM", "مدیریت ریسک"),
            }),
            new("A", "EPMO", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("SY", "سیستم ها و روش ها"),
                new("MD", "توسعه مهارت های طرح ها و پروژه ها"),
                new("PO", "برنامه ریزی و کنترل پروژه ها"),
                new("EC", "برنامه ریزی و کنترل مهندسی"),
                new("PL", "برنامه ریزی و پایش پروژه ها"),
                new("PP", "برنامه ریزی و هزینه پروژه ها"),
            }),
            new("T", "فناوری اطلاعات و حکمرانی داده", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("SD", "تحلیل، طراحی و توسعه سیستم ها"),
                new("CI", "زیرساخت"),
                new("IS", "امنیت اطلاعات"),
                new("TS", "پشتیبانی فنی"),
                new("AI", "هوش مصنوعی"),
            }),
            new("H", "سرمایه انسانی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("TR", "آموزش"),
                new("RH", "برنامه ریزی و توسعه منابع انسانی"),
                new("WI", "امور رفاهی و بیمه کارکنان"),
                new("HR", "امور کارکنان"),
                new("FS", "امور ایاب و ذهاب، تسهیلات و ماشین آلات سبک"),
            }),
            new("Q", "HSEQ", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("PS", "سیستم مدیریت ایمنی فرآیند (PSM)"),
                new("AS", "حفاظت کارکنان"),
                new("QD", "تضمین کیفیت و مدیریت یکپارچه"),
                new("HS", "بهداشت، ایمنی و محیط زیست"),
            }),
            new("E", "طراحی و مهندسی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("EL", "برق و مخابرات"),
                new("IN", "ابزار دقیق"),
                new("CE", "سیویل، سازه و معماری"),
                new("ME", "تجهیزات HVAC"),
                new("PM", "تجهیزات دوار"),
                new("PI", "پایپینگ"),
                new("PR", "فرآیند"),
                new("SF", "ایمنی فرآیند"),
                new("MC", "مواد و خوردگی"),
                new("AR", "توسعه فناوری و شبکه"),
                new("TD", "مهندسی تکنولوژی و شبکه"),
                new("ED", "خدمات مهندسی خاص"),
                new("DC", "کنترل مستندات"),
            }),
            new("P", "بازرگانی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("OP", "سفارشات خارجی و خرید"),
                new("CF", "پیمانکاران فرعی"),
                new("GP", "خریدهای عمومی"),
                new("BS", "پشتیبانی بازرگانی"),
            }),
            new("C", "امور پروژه ها", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("QC", "کنترل کیفیت"),
                new("XP", "ادعا"),
                new("CL", "تعلیق و پیگیری پروژه ها"),
                new("SI", "اجرای پروژه"),
                new("EP", "پیش راه اندازی و راه اندازی"),
                new("PC", "پیش بینی، اندازه گیری و راه داری"),
            }),
            new("F", "مالی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("AM", "حسابداری مدیریت"),
                new("PA", "حسابداری پروژه ها"),
                new("AF", "حسابداری مالی"),
                new("AU", "حسابرسی و کنترل اسناد مالی"),
            }),
            new("R", "روابط عمومی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
            }),
            new("S", "پشتیبانی و انبارها", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("MA", "ماشین آلات"),
                new("EW", "تعمیرات و بازرسی"),
                new("WH", "انبارها"),
            }),
            new("N", "امور حقوقی و قراردادها", new ActivitySeed[]
            {
                new("GE", "عمومی"),
                new("LC", "حقوقی و امور مجامع"),
                new("GI", "تضمین و تنظیم بیمه ها"),
                new("HC", "بررسی پیمان ها"),
                new("OD", "برون سپاری و توسعه بازار و قراردادها"),
                new("TM", "برگزاری مناقصات"),
            }),
            new("I", "حسابرسی داخلی", new ActivitySeed[]
            {
                new("GE", "عمومی"),
            }),
        };

        private record DocumentTypeSeed(string Code, string Title);

        // Transcribed from the company's "List of Document Type Abbreviations" (same source
        // PDF as Data above). NOTE: "Installation Details" and "Strategic Plan" both read as
        // code "ST" in the source image, which cannot both be right (Code is globally unique -
        // see DocumentTypeConfigurations). Kept "Installation Details" = ST here and omitted
        // "Strategic Plan" until the correct code is confirmed against the source document.
        private static readonly DocumentTypeSeed[] DocumentTypeData =
        {
            new("BD", "Block Diagrams"),
            new("CC", "Calculations"),
            new("CP", "Control Philosophy"),
            new("CH", "Charts"),
            new("DB", "Design Basis"),
            new("DC", "Design Criteria"),
            new("DD", "Design Data Book/ Design Dossier"),
            new("DG", "Diagrams"),
            new("DL", "Detail Drawing"),
            new("DM", "Document Register"),
            new("DR", "Drawing"),
            new("DS", "Data Sheet"),
            new("EQ", "Equipment List"),
            new("FM", "Forms"),
            new("GA", "General Arrangements"),
            new("HD", "Hazardous Area Drawing"),
            new("IN", "Instrument Index"),
            new("ST", "Installation Details"),
            new("ID", "Indent"),
            new("IV", "Invoice"),
            new("IS", "Isometric Drawings"),
            new("LD", "Logic Diagrams"),
            new("LT", "Lists"),
            new("LY", "Layouts"),
            new("MA", "Manuals"),
            new("MI", "Miscellaneous Details"),
            new("MR", "Material Requisition"),
            new("MT", "Material Take Off"),
            new("OC", "Organization Chart"),
            new("OM", "Operation Manual"),
            new("PA", "Piping Arrangement"),
            new("PM", "Process Map"),
            new("PF", "Flow Diagrams, Process"),
            new("PI", "P&ID's, Process"),
            new("PY", "Policy"),
            new("PL", "Planning Program"),
            new("PO", "Purchase order"),
            new("PP", "Plot Plants"),
            new("PR", "Procedures"),
            new("QP", "Quality Plan"),
            new("RD", "Requirement of Document"),
            new("RP", "Reports"),
            new("SD", "Standard Drawing"),
            new("SL", "Single Line Diagrams"),
            new("SH", "Schedule"),
            new("SP", "Specifications"),
            new("TF", "Pressure Test Flow Diagrams"),
            new("TB", "Tabulations Bid Analysis"),
            new("TL", "Tabulations"),
            new("UF", "Flow Diagram Utilities"),
            new("WI", "Working Instructions"),
            new("CN", "Contract"),
            new("TP", "Template"),
            new("RE", "Regulation"),
            new("TN", "Tender"),
            new("PC", "Project Charter"),
            new("RC", "RACI Chart"),
        };

        public async Task Seed()
        {
            await SeedOrganizationalManagementsAndActivities();
            await SeedDocumentTypes();
        }

        // Upsert by Code (title-only correction is safe to re-run; nothing here ever
        // deletes a row, since Documents may already reference one by Id).
        private async Task SeedOrganizationalManagementsAndActivities()
        {
            var existingManagements = await _context.Set<OrganizationalManagement>()
                .Include(m => m.Activities)
                .ToListAsync();

            foreach (var managementSeed in Data)
            {
                var management = existingManagements.FirstOrDefault(m => m.Code == managementSeed.Code);
                if (management == null)
                {
                    management = new OrganizationalManagement
                    {
                        Key = Guid.NewGuid(),
                        Code = managementSeed.Code,
                        Title = managementSeed.Title,
                        IsActive = true,
                        CreatedTime = DateTime.UtcNow,
                    };
                    _context.Set<OrganizationalManagement>().Add(management);
                }
                else if (management.Title != managementSeed.Title)
                {
                    management.Title = managementSeed.Title;
                    management.ModifiedDate = DateTime.UtcNow;
                }

                foreach (var activitySeed in managementSeed.Activities)
                {
                    var activity = management.Activities.FirstOrDefault(a => a.Code == activitySeed.Code);
                    if (activity == null)
                    {
                        management.Activities.Add(new OrganizationalActivity
                        {
                            Key = Guid.NewGuid(),
                            Code = activitySeed.Code,
                            Title = activitySeed.Title,
                            IsActive = true,
                            CreatedTime = DateTime.UtcNow,
                        });
                    }
                    else if (activity.Title != activitySeed.Title)
                    {
                        activity.Title = activitySeed.Title;
                        activity.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task SeedDocumentTypes()
        {
            var existingTypes = await _context.Set<DocumentType>().ToListAsync();

            foreach (var typeSeed in DocumentTypeData)
            {
                var documentType = existingTypes.FirstOrDefault(t => t.Code == typeSeed.Code);
                if (documentType == null)
                {
                    _context.Set<DocumentType>().Add(new DocumentType
                    {
                        Key = Guid.NewGuid(),
                        Code = typeSeed.Code,
                        Title = typeSeed.Title,
                        IsActive = true,
                        CreatedTime = DateTime.UtcNow,
                    });
                }
                else if (documentType.Title != typeSeed.Title)
                {
                    documentType.Title = typeSeed.Title;
                    documentType.ModifiedDate = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}