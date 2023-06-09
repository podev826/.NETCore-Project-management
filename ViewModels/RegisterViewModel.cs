﻿using DataRoom.Utilities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DataRoom.ViewModels
{
    public class RegisterViewModel
    {
        [Display(Name = "Representative Name")]
        [Required]
        public string Name { get; set; }

        [Display(Name = "Company Name")]
        [Required]
        public string CompanyName { get; set; }

        [Display(Name = "Street Address")]
        [Required, MaxLength(25, ErrorMessage = "Street name cannot exceed 25 characters")]
        public string StreetAddress { get; set; }

        [Required, MaxLength(25, ErrorMessage = "City name cannot exceed 25 characters")]
        public string City { get; set; }

        [Required, MaxLength(25, ErrorMessage = "Country name cannot exceed 25 characters")]
        public string Country { get; set; }

        [Required]
        [EmailAddress]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        //[ValidEmailDomainAttribute(allowDomain: "icloud.com", ErrorMessage=" Email domain must be icloud.com")]
        public string Email { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password",
            ErrorMessage = "Password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
        public bool Agree { get; set; }

        public bool IsOwner{ get; set; }
    }
}
