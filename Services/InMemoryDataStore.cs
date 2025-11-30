using System.Collections.Concurrent;
using Campus_Virtul_GRLL.Models;

namespace Campus_Virtul_GRLL.Services
{
    /// <summary>
    /// Almacén en memoria para prototipo (sin base de datos).
    /// Thread-safe para operaciones simples de lectura/escritura.
    /// </summary>
    public class InMemoryDataStore
    {
        // Listas principales
        public ConcurrentDictionary<int, Rol> Roles { get; } = new();
        public ConcurrentDictionary<int, Usuario> Usuarios { get; } = new();
        public ConcurrentDictionary<int, Solicitud> Solicitudes { get; } = new();
        public ConcurrentDictionary<int, Curso> Cursos { get; } = new();
        public ConcurrentDictionary<int, Sesion> Sesiones { get; } = new();
        public ConcurrentDictionary<int, SubSeccion> SubSecciones { get; } = new();
        public ConcurrentDictionary<int, Tarea> Tareas { get; } = new();
        public ConcurrentDictionary<int, EntregaTarea> EntregasTarea { get; } = new();
        public ConcurrentDictionary<int, Inscripcion> Inscripciones { get; } = new();

        private int _seqRol = 0;
        private int _seqUsuario = 0;
        private int _seqSolicitud = 0;
        private int _seqCurso = 0;
        private int _seqSesion = 0;
        private int _seqSubSeccion = 0;
        private int _seqTarea = 0;
        private int _seqEntrega = 0;
        private int _seqInscripcion = 0;

        public InMemoryDataStore()
        {
            Seed();
        }

        private void Seed()
        {
            // Roles base
            var adminRol = AddRol("Administrador", "Acceso total", true);
            var profesorRol = AddRol("Profesor", "Gestión de cursos y contenido", true);
            var practicanteRol = AddRol("Practicante", "Acceso a cursos inscritos", true);

            // Usuarios iniciales (contraseñas simples para demo)
            AddUsuario(adminRol.IdRol, "Admin", "Demo", "00000001", "999111111", "admin@demo.local", "Administración", "admin123");
            AddUsuario(profesorRol.IdRol, "Prof", "Ejemplo", "00000002", "999222222", "prof@demo.local", "Formación", "prof123");
            AddUsuario(practicanteRol.IdRol, "Pract", "Test", "00000003", "999333333", "prac@demo.local", "Aprendizaje", "prac123");

            // Curso de ejemplo asignado al profesor
            var curso = AddCurso("Curso Demo", "Curso inicial de prueba", profesorRol.IdRol, Usuarios.Values.First(u => u.IdRol == profesorRol.IdRol && u.DNI == "00000002").IdUsuario);

            var sesion1 = AddSesion(curso.IdCurso, "Introducción", "Conceptos básicos", 1);
            AddSubSeccionContenido(sesion1.IdSesion, "Presentación", "Bienvenido al curso demo.");
            AddSubSeccionVideo(sesion1.IdSesion, "Video Intro", "video_intro.mp4");
            var tarea = AddSubSeccionTarea(sesion1.IdSesion, "Tarea 1", DateTime.Now.AddDays(3));
        }

        #region Generadores ID
        private int NextRolId() => Interlocked.Increment(ref _seqRol);
        private int NextUsuarioId() => Interlocked.Increment(ref _seqUsuario);
        private int NextSolicitudId() => Interlocked.Increment(ref _seqSolicitud);
        private int NextCursoId() => Interlocked.Increment(ref _seqCurso);
        private int NextSesionId() => Interlocked.Increment(ref _seqSesion);
        private int NextSubSeccionId() => Interlocked.Increment(ref _seqSubSeccion);
        private int NextTareaId() => Interlocked.Increment(ref _seqTarea);
        private int NextEntregaId() => Interlocked.Increment(ref _seqEntrega);
        private int NextInscripcionId() => Interlocked.Increment(ref _seqInscripcion);
        #endregion

        #region CRUD Rol/Usuario
        public Rol AddRol(string nombre, string descripcion, bool estado)
        {
            var rol = new Rol
            {
                IdRol = NextRolId(),
                NombreRol = nombre,
                Descripcion = descripcion,
                Estado = estado
            };
            Roles[rol.IdRol] = rol;
            return rol;
        }

        public Usuario AddUsuario(int idRol, string nombres, string apellidos, string dni, string telefono,
            string correo, string area, string password)
        {
            var usuario = new Usuario
            {
                IdUsuario = NextUsuarioId(),
                IdRol = idRol,
                Nombres = nombres,
                Apellidos = apellidos,
                DNI = dni,
                Telefono = telefono,
                CorreoElectronico = correo,
                Area = area,
                PrimerInicio = false,
                ClaveTemporal = password,
                ClavePermanente = password,
                FechaCreacion = DateOnly.FromDateTime(DateTime.Now),
                FechaActualizacion = DateOnly.FromDateTime(DateTime.Now),
                Estado = true,
                Rol = Roles[idRol]
            };
            Usuarios[usuario.IdUsuario] = usuario;
            return usuario;
        }
        #endregion

        #region Solicitudes
        public Solicitud AddSolicitud(int idRol, string nombres, string apellidos, string dni, string telefono,
            string correo, string area)
        {
            var sol = new Solicitud
            {
                IdSolicitud = NextSolicitudId(),
                IdRol = idRol,
                Nombres = nombres,
                Apellidos = apellidos,
                DNI = dni,
                Telefono = telefono,
                CorreoElectronico = correo,
                Area = area,
                FechaSolicitud = DateOnly.FromDateTime(DateTime.Now),
                Estado = EstadoSolicitud.Enviada,
                Rol = Roles[idRol]
            };
            Solicitudes[sol.IdSolicitud] = sol;
            return sol;
        }

        public Usuario AprobarSolicitud(int idSolicitud, string password = "pass123")
        {
            if (!Solicitudes.TryGetValue(idSolicitud, out var sol)) return null!;
            sol.Estado = EstadoSolicitud.Aprobada;
            var usuario = AddUsuario(sol.IdRol, sol.Nombres, sol.Apellidos, sol.DNI, sol.Telefono, sol.CorreoElectronico, sol.Area, password);
            return usuario;
        }

        public bool RechazarSolicitud(int idSolicitud)
        {
            if (!Solicitudes.TryGetValue(idSolicitud, out var sol)) return false;
            sol.Estado = EstadoSolicitud.Rechazada;
            return true;
        }
        #endregion

        #region Cursos
        public Curso AddCurso(string titulo, string descripcion, int idRolProfesor, int idProfesor)
        {
            var curso = new Curso
            {
                IdCurso = NextCursoId(),
                Titulo = titulo,
                Descripcion = descripcion,
                IdProfesor = idProfesor,
                Estado = EstadoCurso.Borrador,
                FechaCreacion = DateTime.Now
            };
            Cursos[curso.IdCurso] = curso;
            return curso;
        }

        public Sesion AddSesion(int idCurso, string titulo, string descripcion, int orden)
        {
            var sesion = new Sesion
            {
                IdSesion = NextSesionId(),
                IdCurso = idCurso,
                Titulo = titulo,
                Descripcion = descripcion,
                Orden = orden
            };
            Sesiones[sesion.IdSesion] = sesion;
            return sesion;
        }

        public SubSeccion AddSubSeccionContenido(int idSesion, string titulo, string texto)
        {
            var sub = new SubSeccion
            {
                IdSubSeccion = NextSubSeccionId(),
                IdSesion = idSesion,
                Tipo = TipoSubSeccion.Contenido,
                Titulo = titulo,
                Texto = texto
            };
            SubSecciones[sub.IdSubSeccion] = sub;
            return sub;
        }

        public SubSeccion AddSubSeccionVideo(int idSesion, string titulo, string rutaVideo)
        {
            var sub = new SubSeccion
            {
                IdSubSeccion = NextSubSeccionId(),
                IdSesion = idSesion,
                Tipo = TipoSubSeccion.Video,
                Titulo = titulo,
                RutaVideo = rutaVideo
            };
            SubSecciones[sub.IdSubSeccion] = sub;
            return sub;
        }

        public SubSeccion AddSubSeccionTarea(int idSesion, string titulo, DateTime fechaLimite)
        {
            var tarea = new Tarea
            {
                IdTarea = NextTareaId(),
                FechaLimite = fechaLimite,
                Estado = EstadoTarea.Activa
            };
            Tareas[tarea.IdTarea] = tarea;
            var sub = new SubSeccion
            {
                IdSubSeccion = NextSubSeccionId(),
                IdSesion = idSesion,
                Tipo = TipoSubSeccion.Tarea,
                Titulo = titulo,
                IdTarea = tarea.IdTarea
            };
            SubSecciones[sub.IdSubSeccion] = sub;
            return sub;
        }
        #endregion

        #region Inscripciones
        public Inscripcion SolicitarInscripcion(int idCurso, int idUsuario)
        {
            // Evitar duplicado
            var existente = Inscripciones.Values.FirstOrDefault(i => i.IdCurso == idCurso && i.IdUsuario == idUsuario);
            if (existente != null) return existente;
            var ins = new Inscripcion
            {
                IdInscripcion = NextInscripcionId(),
                IdCurso = idCurso,
                IdUsuario = idUsuario,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoInscripcion.Pendiente
            };
            Inscripciones[ins.IdInscripcion] = ins;
            return ins;
        }

        public bool CambiarEstadoInscripcion(int idInscripcion, EstadoInscripcion nuevoEstado)
        {
            if (!Inscripciones.TryGetValue(idInscripcion, out var ins)) return false;
            ins.Estado = nuevoEstado;
            ins.FechaRespuesta = DateTime.Now;
            return true;
        }
        #endregion

        #region Entregas
        public EntregaTarea EntregarArchivo(int idTarea, int idUsuario, string ruta)
        {
            var tarea = Tareas[idTarea];
            var entrega = new EntregaTarea
            {
                IdEntregaTarea = NextEntregaId(),
                IdTarea = idTarea,
                IdUsuario = idUsuario,
                RutaArchivo = ruta,
                FechaEntrega = DateTime.Now,
                Estado = DateTime.Now <= tarea.FechaLimite ? EstadoEntrega.Entregada : EstadoEntrega.FueraDeTiempo
            };
            EntregasTarea[entrega.IdEntregaTarea] = entrega;
            return entrega;
        }

        public bool CalificarEntrega(int idEntrega, int nota)
        {
            if (!EntregasTarea.TryGetValue(idEntrega, out var entrega)) return false;
            entrega.Nota = nota;
            entrega.Estado = EstadoEntrega.Calificada;
            return true;
        }
        #endregion
    }
}
