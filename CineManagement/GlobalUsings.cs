// GlobalUsings.cs - Modular Monolith Unified Namespaces
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Data & Core
global using CineManagement.Data;
global using CineManagement.Models;
global using CineManagement.Controllers;
global using CineManagement.Common;
global using CineManagement.DTOs;
global using CineManagement.Services;

// Modular Boundaries: Cinemas
global using CineManagement.Modules.Cinemas;
global using CineManagement.Modules.Cinemas.Domain;
global using CineManagement.Modules.Cinemas.DTOs;
global using CineManagement.Modules.Cinemas.Services;

// Modular Boundaries: Catalog
global using CineManagement.Modules.Catalog;
global using CineManagement.Modules.Catalog.Domain;
global using CineManagement.Modules.Catalog.Services;

// Modular Boundaries: Ticketing
global using CineManagement.Modules.Ticketing;
global using CineManagement.Modules.Ticketing.Domain;
global using CineManagement.Modules.Ticketing.Services;

// Modular Boundaries: Ordering
global using CineManagement.Modules.Ordering;
global using CineManagement.Modules.Ordering.Domain;
global using CineManagement.Modules.Ordering.Services;

// Modular Boundaries: Payments
global using CineManagement.Modules.Payments;
global using CineManagement.Modules.Payments.Domain;
global using CineManagement.Modules.Payments.Services;

// Modular Boundaries: Shared
global using CineManagement.Modules.Shared;
global using CineManagement.Modules.Shared.Storage;
global using CineManagement.Modules.Shared.Common;
global using CineManagement.Shared;
