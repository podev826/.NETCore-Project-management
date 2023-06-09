﻿using DataRoom.Models;
using DataRoom.Utilities;
using DataRoom.Service.Interface;
using DataRoom.ViewModels;
using FileUploadDownload.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataRoom.Controllers
{
    public class HomeController : Controller
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IHostEnvironment _hostingEnvironment;
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private ApplicationUser _currentuser;
        private readonly IEmailService _emailService = null;

        public HomeController(IEmployeeRepository employeeRepository,
                              IHostEnvironment hostingEnvironment, AppDbContext context, 
                              UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _employeeRepository = employeeRepository;
            _hostingEnvironment = hostingEnvironment;
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        /// <summary>
        /// List files currently on the upload folder of the server
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            // Get files from the server
            var model = new FilesViewModel();

            var directories = new List<String> { };
            
            List<Project> projects = new List<Project> { };
            
            _currentuser = _userManager.GetUserAsync(User).Result;

            // List all projects
            if (User.IsInRole("Admins"))
                projects = _context.Project.ToList();

            // List only project that project ownder belongs
            if(User.IsInRole("Owners"))
            {
                projects.AddRange(_context.Project.Where(p => p.OwnerId == _currentuser.Id).ToList());
            }

            // List only files/folders that bidder belongs
            if (User.IsInRole("Bidders"))
            {
                var bidderProject = _context.BidderProjects.Where(b=>b.BidderId==_currentuser.Id).Select(b => b.ProjectId).ToList();
                projects.AddRange(_context.Project.Where(p => bidderProject.Contains(p.Id)).ToList());
            }

            foreach (var project in projects)
            {
                model.Directories.Add(new DirectoryDetails
                {
                    Name = project.Name,
                    Path = Path.Combine(Directory.GetCurrentDirectory(), "upload/" + project.Name)
                });
            }

            foreach (var item in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "upload")))
            {
                model.Files.Add(new FileDetails 
                { 
                    Name = System.IO.Path.GetFileName(item), 
                    Path = item 
                });
            }
            
            return View(model);
        }

        /// <summary>
        /// List files for project
        /// </summary>
        /// <returns></returns>
        public IActionResult Project(string projectName)
        {
            // Get files from the server
            var model = new FilesViewModel();
            var directories = new List<String> { };
            var project = _context.Project.Where(p => p.Name == projectName).FirstOrDefault();

            // First get all bidders of the project
            List<BidderProject> bidders = _context.BidderProjects.Where(b => b.ProjectId == project.Id).ToList();

            _currentuser = _userManager.GetUserAsync(User).Result;

            // If user role is Bidders then filter bidders, then add bidder's directory to model
            if (User.IsInRole("Bidders"))
            {
                bidders = bidders.Where(b => b.BidderId == _currentuser.Id).ToList();
            }

            if (bidders != null)

                foreach (var b in bidders)
                {
                    var bidder = _context.Users.Where(u => u.Id == b.BidderId).First().UserName;

                    // Creates bidder folder as its username in upload folder for example sthay
                    model.Directories.Add(new DirectoryDetails
                    {
                        Name = bidder,
                        Path = Path.Combine(Directory.GetCurrentDirectory(), "upload/" + bidder)
                    });
                }

            // Lists files belong to project folder
            foreach (var item in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "upload/" + projectName)))
            {
                model.Files.Add(new FileDetails 
                { 
                    Name = System.IO.Path.GetFileName(item), Path = item 
                });
            }
            
            ViewBag.projectName = projectName;

            return View(model);
        }

        /// <summary>
        /// List files for Bidder
        /// </summary>
        /// <returns></returns>
        public IActionResult Bidder(string projectName, string bidderName = null)
        {
            // Get files from the server
            var model = new FilesViewModel();

            _currentuser = _userManager.GetUserAsync(User).Result;

            var project = _context.Project.Where(p => p.Name == projectName).FirstOrDefault();

            foreach (var item in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "upload/" + projectName + "/" + bidderName)))
            {
                model.Files.Add(new FileDetails 
                { 
                    Name = System.IO.Path.GetFileName(item), 
                    Path = item 
                });
            }

            ViewBag.projectName = projectName;
            ViewBag.bidderName = bidderName;
            
            return View(model);
        }



        [HttpGet]
        public IActionResult UploadFiles(string path)
        {
            ViewBag.path = path;
            return View();
        }

        [HttpPost]
        public IActionResult UploadFiles(IFormFile[] filearray, Double[] filesize, string path)
        {
            // Iterate each files
            var illegalFiles = new List<string> { };
            var uploadedFileCount = 0;
            foreach (var file in filearray)
            {
                // Gets the file name from the browser
                var fileName = System.IO.Path.GetFileName(file.FileName);

                // Gets file path to be uploaded
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), path, fileName);
                
                if(GetContentType(filePath)==null)
                {
                    illegalFiles.Add(fileName);
                    continue;
                }
                
                // Checks If file with same name exists and delete it
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Creates a new local file and copy contents of uploaded file
                using (var localFile = System.IO.File.OpenWrite(filePath))
                
                using (var uploadedFile = file.OpenReadStream())
                {
                    uploadedFile.CopyTo(localFile);
                    uploadedFileCount++;
                }
            }

            var failedResponse = new List<String> { };

            if (uploadedFileCount > 0)
            {
                var projectName = path.Split("/")[1];
                var project = _context.Project.Where(p => p.Name == projectName).First();
                if(User.IsInRole("Owners"))
                    failedResponse = sendEmailToBidders(project);
                if(User.IsInRole("Bidders"))
                    failedResponse = sendEmailToOwner(project);
            }

            if (illegalFiles.Count == 0 && failedResponse.Count == 0)
            {
                ViewBag.Message = "Files are successfully uploaded";
                return Json(new { status = "success" });
            }
            else
                return Json(new { status = "failed", failedFiles = illegalFiles, failedEmails = failedResponse });

        }

        private List<string> sendEmailToOwner(Project project)
        {
            var owner = _context.Users.Where(u => u.Id == project.OwnerId).FirstOrDefault();
            var projectLink = Url.Action("project", "Home", new { projectName = project.Name }, Request.Scheme);
            var res = new List<String>();

            string subjectLine = $"{User.Identity.Name} uploaded project file(s) to your project({project.Name}).";
            
            var response = _emailService.SendEmail(owner.Email, subjectLine, projectLink);

            if (!response)
                res.Add(owner.UserName);
            return res;
        }

        private List<string> sendEmailToBidders(Project project)
        {
            var bidders = _context.BidderProjects.Where(b => b.ProjectId == project.Id).Select(b => b.Bidder).ToList();
            var projectLink = Url.Action("project", "Home", new { projectName = project.Name }, Request.Scheme);
            var res = new List<String>();

            string subjectLine = "New project file(s) uploaded by project owner.";

            foreach (var bidder in bidders)
            {
                var response = _emailService.SendEmail(bidder.Email, subjectLine, projectLink);

                if (!response)
                    res.Add(bidder.UserName);
            }
            return res;
        }

        // Downloads file from the server
        public async Task<IActionResult> Download(string filename)
        {
            if (filename == null)
                return Content("filename is not availble");

            var path = Path.Combine(Directory.GetCurrentDirectory(), "upload", filename);

            var memory = new MemoryStream();

            using (var stream = new FileStream(path, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            
            memory.Position = 0;
            
            return File(memory, GetContentType(path), Path.GetFileName(path));
        }

        // Gets content type
        private string? GetContentType(string path)
        {
            var types = GetMimeTypes();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            
            if (types.ContainsKey(ext))
                
                return types[ext];
            
            else
                
                return null;
        }

        // Gets mime types
        private Dictionary<string, string> GetMimeTypes()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "static", "mimetype.txt");
            var readFile =  System.IO.File.ReadAllText(path);
            var mimeTypes = new Dictionary<string, string> { };
            foreach(var mimeType in readFile.Split('\n'))
            {
                var key = mimeType.Split(',')[0].Trim();
                var value = mimeType.Split(',')[1].Trim();
                mimeTypes.Add(key, value);
            }
            return mimeTypes;
        }

        public ViewResult GetAllEmployees()
        {
            // retrieve all the employees
            var model = _employeeRepository.GetAllEmployees();
            
            // Pass the list of employees to the view
            return View(model);
        }

        public ViewResult Detail(int id)
        {
            EmployeeDetailViewModel detailViewModel = new EmployeeDetailViewModel()
            {
                Employee = _employeeRepository.GetEmployee(id),
                PageTitle = "Employee Details"
            };
            
            return View(detailViewModel);
        }

        [HttpGet]
        public ViewResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(EmployeeCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                string uniqueFileName = null;

                // If the Photo property on the incoming model object is not null, then the user
                // has selected an image to upload.
                if (model.Photo != null)
                {
                    // The image must be uploaded to the images folder in wwwroot
                    // To get the path of the wwwroot folder we are using the inject
                    // HostingEnvironment service provided by ASP.NET Core
                    string uploadsFolder = Path.Combine(_hostingEnvironment.ContentRootPath, "images");
                    
                    // To make sure the file name is unique we are appending a new
                    // GUID value and and an underscore to the file name
                    uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Photo.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    // Use CopyTo() method provided by IFormFile interface to
                    // copy the file to wwwroot/images folder
                    model.Photo.CopyTo(new FileStream(filePath, FileMode.Create));
                }

                Employee newEmployee = new Employee
                {
                    Name = model.Name,
                    Email = model.Email,
                    Department = (Departments) model.Department,
                    
                    // Store the file name in PhotoPath property of the employee object
                    // which gets saved to the Employees database table
                    PhotoPath = uniqueFileName
                };

                _employeeRepository.Add(newEmployee);
                return RedirectToAction("GetEmployee", new { id = newEmployee.Id });
            }

            return View();
        }
    }
}
