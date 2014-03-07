using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using Countersoft.Foundation.Commons.Extensions;
using Countersoft.Gemini.Commons;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.Permissions;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Gemini.Infrastructure.Apps;
using Countersoft.Gemini.Models;
using System.Linq;
using System.Text;
using Countersoft.Foundation.Commons.Enums;
using Countersoft.Gemini.Commons.Entity.Security;
using Countersoft.Gemini.Commons.Meta;
using Countersoft.Gemini.Infrastructure.Grid;
using System.ComponentModel.DataAnnotations;
using Countersoft.Gemini.Infrastructure.Managers;
using Countersoft.Foundation.Utility.Helpers;
using Countersoft.Gemini;

namespace DocStore
{
    // This controller will consume the ajax posts from the view item and main config pages. See routes class at the end of this file
    [AppType(AppTypeEnum.FullPage),
    AppGuid("1B9CB627-A2F2-4CC5-BE5B-D0FABB489F87"),
    AppControlGuid("67AAE97B-C366-4203-86BA-0A2110700261"),
    AppAuthor("Countersoft"), AppKey("DocStore"),
    AppName("DocStore"), AppDescription("DocStore"),
    AppControlUrl("view"),
    AppRequiresViewPermission(true),
    AppRequiresEditPermission(true)]
    [OutputCache(Duration = 0, NoStore = true, Location = System.Web.UI.OutputCacheLocation.None)]
    public class Documents : BaseAppController
    {
        public ProjectManager _projectManager { get; set; }

        public ProjectDocumentManager _projectDocumentManager { get; set; }

        public override void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(null, "apps/documents/LoadTree/{projectId}", new { controller = "Documents", action = "LoadTree" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/UploadFile/{projectId}", new { controller = "Documents", action = "UploadFile" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/DeleteDocument/{projectId}", new { controller = "Documents", action = "DeleteDocument" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/savedescription/{projectId}", new { controller = "Documents", action = "savedescription" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/GetFolderContents/{projectId}", new { controller = "Documents", action = "GetFolderContents" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/rename/{projectId}", new { controller = "Documents", action = "Rename" }, new { projectId = @"\d{1,10}" });
            
            routes.MapRoute(null, "apps/documents/DeleteFolder/{projectId}", new { controller = "Documents", action = "DeleteFolder" }, new { projectId = @"\d{1,10}" });
        }

        public override WidgetResult Caption(IssueDto issue)
        {
            return new WidgetResult() { Success = true, Markup = new WidgetMarkup("DocStore") };
        }

        public override WidgetResult Show(IssueDto args)
        {
            _projectManager = new ProjectManager(Cache, UserContext, GeminiContext);
            
            _projectDocumentManager = new ProjectDocumentManager(Cache, UserContext, GeminiContext);

            IssuesGridFilter tmp = new IssuesGridFilter();
            
            HttpSessionManager HttpSessionManager = new HttpSessionManager();

            var projects = new List<int>();
            
            int selectedFolderKey = 0;

            // Safety check required because of http://gemini.countersoft.com/project/DEV/21/item/5088
            try
            {
                if (CurrentCard.IsNew)
                {
                    tmp = new IssuesGridFilter(HttpSessionManager.GetFilter(CurrentCard.Id, CurrentCard.Filter));
                    
                    if (tmp == null)
                    {
                        tmp = CurrentCard.Options[AppGuid].FromJson<IssuesGridFilter>();
                    }
                    
                    projects = tmp.GetProjects();
                }
                else
                {
                    var cardOptions = CurrentCard.Options[AppGuid].FromJson<DocumentAppNavCard>();
                    
                    projects.Add(cardOptions.projectId);
                    
                    selectedFolderKey = cardOptions.folderKey;
                }
            }
            catch (Exception ex)
            {
                tmp = new IssuesGridFilter(HttpSessionManager.GetFilter(CurrentCard.Id, IssuesFilter.CreateProjectFilter(UserContext.User.Entity.Id, UserContext.Project.Entity.Id)));
                
                projects = tmp.GetProjects();
            }

            var activeProjects = _projectManager.GetActive();
            
            var viewableProjects = new List<ProjectDto>();

            int? currentProjectId = projects.Count > 0 ? projects.First() : 0;

            if (activeProjects == null || activeProjects.Count == 0)
            {
                activeProjects = new List<ProjectDto>();
            }
            else
            {
                viewableProjects = ProjectManager.GetAppViewableProjects(this);
            }

            if (!viewableProjects.Any(s => s.Entity.Id == currentProjectId.Value))
            {
                currentProjectId = viewableProjects.Count > 0 ? viewableProjects.First().Entity.Id : 0;
            }

            var model = new DocumentsModel() { Projects = viewableProjects, FolderList = _projectDocumentManager.GetFolders(currentProjectId) };
            
            model.ProjectList = new SelectList(model.Projects, "Entity.Id", "Entity.Name", currentProjectId);
            
            model.Projects = viewableProjects;

            model.SelectedFolder = selectedFolderKey;

            model.HeaderText.Add(new HeaderTextItem(ResourceKeys.Documents));

            if (!model.FolderList.Any() && currentProjectId > 0)
            {
                var entity = new ProjectDocument
                {
                    Name = ResourceKeys.Documents,
                    IsFolder = true,
                    ProjectId = currentProjectId 
                };

                model.FolderList.Add(_projectDocumentManager.Create(entity));
            }

            if (ProjectManager.GetAppEditableProjects(this).Count > 0)
            {
                ViewBag.EditPermission=true;
            }
            else
            {
                ViewBag.EditPermission=false;
            }

            WidgetResult result = new WidgetResult();
            
            result.Markup = new WidgetMarkup("views\\Documents.cshtml", model);
            
            result.Success = true;

            return result;
        }

        public ActionResult LoadTree(int projectId)
        {
            if (!CanViewProject(projectId)) return JsonNoPermissions();

            var documents = ProjectDocumentManager.GetFolders(projectId);

            if (documents == null || documents.Count == 0)
            {
                var entity = new ProjectDocument
                {
                    Name = GetResource(ResourceKeys.Documents),
                    IsFolder = true,
                    ProjectId = projectId
                };
                
                documents.Add(ProjectDocumentManager.Create(entity));
            }

            var result = documents.Select(document => new FolderItem(document)).ToList();

            return Content(result.ToJson(), "text/json");
        }

        /// <summary>
        /// Rename a node on the tree, either a new one created, or an existing one.
        /// </summary>
        /// <param name="key"> id of the folder, 0 for root, -1 for new folder</param>
        /// <param name="name">New folder name</param>
        /// <param name="parent">Id of the parent, used for creating new folders</param>
        /// <returns></returns>
        public ActionResult Rename(int key, string name, int? parent, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            ProjectDocumentDto document;

            if (key == -1)
            {

                var doc = new ProjectDocument
                {
                    Name = name,
                    IsFolder = true,
                    ParentDocumentId = parent,
                    ProjectId = projectId
                };

                document = ProjectDocumentManager.Create(doc);
            }
            else
            {
                document = ProjectDocumentManager.Get(key);

                document.Entity.Name = name;

                ProjectDocumentManager.Update(document.Entity);
            }

            return JsonSuccess(document);
        }

        public ActionResult Delete(int key, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            ProjectDocumentManager.Delete(key);

            return JsonSuccess(null);
        }
        
        public ActionResult GetFolderContents(int key, int projectId)
        {
            if (!CanViewProject(projectId)) return JsonNoPermissions();

            List<ProjectDocumentDto> projectDocuments = ProjectDocumentManager.GetDocuments(projectId, key, LoadPattern.Partial).Where(d => !d.Entity.IsFolder).ToList();
            
            var readOnly = false;
            
            if (!CanManageProject(projectId))
            {
                readOnly = true;
            }
          
            var markup = new Grid<ProjectDocumentDto>(projectDocuments, new ViewContext())
                .RenderHeaders(false)
                .Attributes(@style => "width:100%", @class => "cs-template", @id => "documents")
                .HeaderRowAttributes(new Dictionary<string, object> { { "class", "align-left cs-header" } })
                .RowAttributes(file => new Dictionary<string, object> { { "data-id", file.Item.Entity.Id } })
                .Columns(
                        col =>
                        {
                            col.Custom(x => RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_DocumentListFileIcon.cshtml"), x))
                                .Attributes(@class => "cs-filename no-wrap no-width")
                                .Named(ResourceKeys.FileName);

                            col.For(x => x.Entity.Description)
                                .Named(ResourceKeys.Description)
                                .Attributes(@class => "cs-title");

                            col.For(x => x.Entity.FileSize)
                                .Named(ResourceKeys.FileSize)
                                .Attributes(@class => "cs-filesize no-wrap no-width");

                            col.For(x => x.Entity.Created)
                                .Named(ResourceKeys.Created)
                                .Attributes(@class => "cs-filecreated no-wrap no-width");
                            if (!readOnly)
                            {
                                col.Custom(x => RenderPartialViewToString(this, AppManager.Instance.GetAppUrl(Constants.AppId, "views/_DocumentListDelete.cshtml"), x))
                                    .Attributes(@class => "cs-icons  no-wrap no-width")
                                    .Named(string.Empty);
                            }
                        }
                    )
                .Empty(string.Concat("<div class='center'>", "<img src='", UserContext.Url, "/assets/images/apps/1B9CB627-A2F2-4CC5-BE5B-D0FABB489F87/no-docs.png' alt='no docs'>", "</div>"));

            return Content(markup.ToHtmlString());
        }

        public ActionResult UploadFile(int? folderId, string qqfile, int? documentId, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            var attachment = documentId.HasValue ? ProjectDocumentManager.Get(documentId.Value).Entity : new ProjectDocument();

            if (String.IsNullOrEmpty(Request["qqfile"]) && Request.Files != null && Request.Files.Count > 0)
            {
                // IE
                qqfile = Request.Files[0].FileName;
                
                attachment.Name = qqfile;
                
                attachment.ProjectId = projectId;
                
                attachment.ContentLength = (int)Request.Files[0].InputStream.Length;
                
                attachment.ContentType = Request.Files[0].ContentType; 
                
                attachment.Created = DateTime.UtcNow;

                var fileContent = new Byte[attachment.ContentLength];

                Request.Files[0].InputStream.Read(fileContent, 0, attachment.ContentLength);
                
                attachment.Data = fileContent;
            }
            else
            {
                attachment.ProjectId = projectId;
                
                attachment.ContentLength = (int)Request.InputStream.Length;
                
                attachment.ContentType = ContentHelper.GetFileContentType(FileHelper.GetFileExtension(qqfile));
                
                attachment.Name = qqfile;
                
                attachment.Created = DateTime.UtcNow;

                var fileContent = new Byte[attachment.ContentLength];

                Request.InputStream.Read(fileContent, 0, attachment.ContentLength);
                
                attachment.Data = fileContent;
            }

            if (string.IsNullOrEmpty(qqfile)) return JsonError();

            if (documentId.HasValue)
            {
                ProjectDocumentManager.Update(attachment);
            }
            else
            {
                var folder = ProjectDocumentManager.Get(folderId.Value);
                
                attachment.ParentDocumentId = folder.Entity.Id;
                
                ProjectDocumentManager.Create(attachment);
            }

            return Json(new { success = true, fileId = attachment.Id, folderId = attachment.ParentDocumentId }, "text/html");
        }

        public ActionResult DeleteDocument(int documentId, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            var documentDto = ProjectDocumentManager.Get(documentId);
            
            if (documentDto != null)
            {
                ProjectDocumentManager.Delete(documentId);
                
                return JsonSuccess(new { id = documentId });
            }
            
            return JsonError("Unable to find file");
        }

        public ActionResult DeleteFolder(int folderId, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            var documentDto = ProjectDocumentManager.Get(folderId);

            if (documentDto != null)
            {
                if (!documentDto.Entity.IsFolder)
                    throw new ValidationException("Not a folder");

                ProjectDocumentManager.Delete(folderId);
                
                return JsonSuccess(new { id = folderId });
            }

            return JsonError("Unable to find folder");
        }

        public ActionResult Download(int fileId, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            ProjectDocument attachment = GeminiContext.ProjectDocuments.Get(fileId);
            
            if (attachment == null || attachment.IsNew) return Redirect("~/");
            
            if (attachment.ContentType.IsEmpty()) attachment.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
            
            return File(attachment.Data, attachment.ContentType, attachment.Name);
        }

        public ActionResult  SaveDescription(int fileId, string description, int projectId)
        {
            if (!CanManageProject(projectId)) return JsonNoPermissions();

            ProjectDocument attachment = GeminiContext.ProjectDocuments.Get(fileId);
            
            if (attachment == null || attachment.IsNew) return Redirect("~/");

            BeginTransaction();
            
            DateTime updateTime = DateTime.UtcNow;
            
            attachment.Description = description;
            
            attachment.Created = updateTime;

            attachment = ProjectDocumentManager.Update(attachment).Entity;
            
            return JsonSuccess(attachment.Created.ToString());
        }

        public bool CanManageProject(int projectId)
        {
            var data = PermissionSetManager.GetAppPermissions(Constants.AppId);

            if (data == null) return false;

            if (!data.Projects.Contains(Countersoft.Gemini.Commons.Constants.AllProjectsId) && !data.Projects.Contains(projectId)) return false;

            var projects = ProjectManager.GetActive();

            if (projects == null || projects.Count == 0 || (projects.Count > 0 && !projects.Any(p => p.Entity.Id == projectId))) return false;

            if (!UserContext.User.ProjectGroups.Any(s => data.EditGroups.Contains(s.Id) || !s.MembersForProject(projectId).Any())) return false;

            return true;
        }

        public bool CanViewProject(int projectId)
        {
            var data = PermissionSetManager.GetAppPermissions(Constants.AppId);

            if (data == null) return false;

            if (!data.Projects.Contains(Countersoft.Gemini.Commons.Constants.AllProjectsId) && !data.Projects.Contains(projectId)) return false;

            var projects = ProjectManager.GetActive();

            if (projects == null || projects.Count == 0 || !projects.Any(p => p.Entity.Id == projectId)) return false;

            if (!UserContext.User.ProjectGroups.Any(s => data.ViewGroups.Contains(s.Id) || !s.MembersForProject(projectId).Any())) return false;

            return true;
        }
    }

    public class DocumentAppNavCard
    {
        public int projectId { get; set; }
        public int folderKey { get; set; }
    }
}
