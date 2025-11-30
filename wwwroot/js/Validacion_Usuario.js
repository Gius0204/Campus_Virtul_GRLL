window.addEventListener("load", () => {
  console.log("JavaScript de Login cargado");

  const form = document.getElementById("formulario");
  const dni = document.getElementById("dni");
  const contrasena = document.getElementById("contrasena");
  const togglePassword = document.getElementById("togglePassword");

  // ============================================
  // BOTÓN MOSTRAR/OCULTAR CONTRASEÑA
  // ============================================
  if (togglePassword && contrasena) {
    togglePassword.addEventListener("click", function () {
      const type =
        contrasena.getAttribute("type") === "password" ? "text" : "password";
      contrasena.setAttribute("type", type);
      this.textContent = type === "password" ? "🙈" : "👁️";
      console.log("👁️ Contraseña:", type === "password" ? "oculta" : "visible");
    });
  }

  // Límites de caracteres
  const LIMITES = {
    DNI: 8,
    CONTRASENA_MIN: 4, // mínimo real permitido
    CONTRASENA_MAX: 20, // coincide con maxlength del input
  };

  dni.addEventListener("input", (e) => {
    e.target.value = e.target.value.replace(/[^\d]/g, "").slice(0, LIMITES.DNI);
  });

  form.addEventListener("submit", (e) => {
    console.log("Submit detectado");
    e.preventDefault();
    validaCampos();
  });

  const validaCampos = () => {
    const dniValor = dni.value.trim();
    const contrasenaValor = contrasena.value.trim();

    let esValido = true;

    if (dniValor === "") {
      validaFalla(dni, "El campo DNI es obligatorio");
      esValido = false;
    } else if (dniValor.length !== LIMITES.DNI) {
      validaFalla(dni, `El DNI debe tener exactamente ${LIMITES.DNI} dígitos`);
      esValido = false;
    } else if (!/^\d{8}$/.test(dniValor)) {
      validaFalla(dni, "El DNI solo debe contener números");
      esValido = false;
    } else {
      validaOK(dni);
    }

    if (contrasenaValor === "") {
      validaFalla(contrasena, "El campo contraseña es obligatorio");
      esValido = false;
    } else if (contrasenaValor.length < LIMITES.CONTRASENA_MIN) {
      validaFalla(
        contrasena,
        `La contraseña debe tener al menos ${LIMITES.CONTRASENA_MIN} caracteres`
      );
      esValido = false;
    } else if (contrasenaValor.length > LIMITES.CONTRASENA_MAX) {
      validaFalla(
        contrasena,
        `La contraseña no debe exceder ${LIMITES.CONTRASENA_MAX} caracteres`
      );
      esValido = false;
    } else {
      validaOK(contrasena);
    }

    console.log("¿Es válido?:", esValido);

    if (esValido) {
      console.log("✅ Enviando formulario al servidor...");
      form.submit();
    }
  };

  // Mostrar error
  const validaFalla = (input, mensaje) => {
    const inputBox = input.closest(".input-box") || input.parentElement;
    const avisoAnterior = inputBox.querySelector(".aviso");
    if (avisoAnterior) avisoAnterior.remove();

    const aviso = document.createElement("small");
    aviso.className = "aviso";
    aviso.innerText = mensaje;

    inputBox.appendChild(aviso);
    input.style.borderColor = "#e74c3c";
  };

  // Limpiar error
  const validaOK = (input) => {
    const inputBox = input.closest(".input-box") || input.parentElement;
    const aviso = inputBox.querySelector(".aviso");
    if (aviso) aviso.remove();
    input.style.borderColor = "#28a745";
  };

  // Validación en blur
  if (dni) {
    dni.addEventListener("blur", () => {
      const valor = dni.value.trim();
      if (valor === "") return;
      if (valor.length === LIMITES.DNI && /^\d{8}$/.test(valor)) {
        validaOK(dni);
      } else {
        validaFalla(
          dni,
          `El DNI debe tener exactamente ${LIMITES.DNI} dígitos numéricos`
        );
      }
    });

    dni.addEventListener("focus", () => {
      const inputBox = dni.parentElement;
      const aviso = inputBox.querySelector(".aviso");
      if (aviso) aviso.remove();
      dni.style.borderColor = "#e0e0e0";
    });
  }

  if (contrasena) {
    contrasena.addEventListener("blur", () => {
      const valor = contrasena.value.trim();
      if (valor === "") return;
      if (valor.length < LIMITES.CONTRASENA_MIN) {
        validaFalla(contrasena, `Mínimo ${LIMITES.CONTRASENA_MIN} caracteres`);
      } else if (valor.length > LIMITES.CONTRASENA_MAX) {
        validaFalla(contrasena, `Máximo ${LIMITES.CONTRASENA_MAX} caracteres`);
      } else {
        validaOK(contrasena);
      }
    });

    contrasena.addEventListener("focus", () => {
      const inputBox =
        contrasena.closest(".input-box") || contrasena.parentElement;
      const aviso = inputBox.querySelector(".aviso");
      if (aviso) aviso.remove();
      contrasena.style.borderColor = "#e0e0e0";
    });
  }

  console.log("🎉 Todas las funcionalidades inicializadas");
});

// ============================================
// RECUPERACIÓN DE CONTRASEÑA
// ============================================
const modalRecuperacion = document.getElementById("modalRecuperacion");
const forgotPasswordLink = document.getElementById("forgotPasswordLink");
const closeModal = document.getElementById("closeModal");
const cancelarRecuperacion = document.getElementById("cancelarRecuperacion");
const formRecuperacion = document.getElementById("formRecuperacion");
const btnEnviarRecuperacion = document.getElementById("btnEnviarRecuperacion");

// Abrir modal
if (forgotPasswordLink) {
  forgotPasswordLink.addEventListener("click", function (e) {
    e.preventDefault();
    modalRecuperacion.classList.add("active");
    document.getElementById("correoRecuperacion").focus();
    console.log("📧 Modal de recuperación abierto");
  });
}

// Cerrar modal con X
if (closeModal) {
  closeModal.addEventListener("click", function () {
    modalRecuperacion.classList.remove("active");
    formRecuperacion.reset();
    console.log("❌ Modal cerrado con X");
  });
}

// Cerrar modal con botón Cancelar
if (cancelarRecuperacion) {
  cancelarRecuperacion.addEventListener("click", function () {
    modalRecuperacion.classList.remove("active");
    formRecuperacion.reset();
    console.log("❌ Modal cerrado con Cancelar");
  });
}

// Cerrar modal al hacer clic fuera
if (modalRecuperacion) {
  modalRecuperacion.addEventListener("click", function (e) {
    if (e.target === modalRecuperacion) {
      modalRecuperacion.classList.remove("active");
      formRecuperacion.reset();
      console.log("❌ Modal cerrado al hacer clic fuera");
    }
  });
}

// Cerrar modal con tecla ESC
document.addEventListener("keydown", function (e) {
  if (
    e.key === "Escape" &&
    modalRecuperacion &&
    modalRecuperacion.classList.contains("active")
  ) {
    modalRecuperacion.classList.remove("active");
    formRecuperacion.reset();
    console.log("❌ Modal cerrado con ESC");
  }
});

// Envío del formulario de recuperación
if (formRecuperacion) {
  formRecuperacion.addEventListener("submit", function (e) {
    const correo = document.getElementById("correoRecuperacion").value.trim();

    if (!correo || !correo.includes("@") || !correo.includes(".")) {
      e.preventDefault();
      alert("Por favor, ingrese un correo electrónico válido.");
      return false;
    }

    // Mostrar loading
    btnEnviarRecuperacion.classList.add("loading");
    btnEnviarRecuperacion.disabled = true;
    console.log("📤 Enviando solicitud de recuperación...");

    // El formulario se enviará normalmente al servidor
    return true;
  });
}
