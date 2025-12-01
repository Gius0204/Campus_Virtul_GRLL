window.addEventListener("load", () => {
  const form = document.getElementById("formulario");
  const rolSelect = document.getElementById("rol");
  const nombres = document.getElementById("nombres");
  const apellidos = document.getElementById("apellidos");
  const dni = document.getElementById("dni");
  const telefono = document.getElementById("telefono");
  const correoElectronico = document.getElementById("correElectronico");
  const area = document.getElementById("area");

  console.log("JavaScript cargado correctamente");
  console.log("Formulario encontrado:", form);
  console.log("Action del formulario:", form?.action);

  // Límites de caracteres
  const LIMITES = {
    NOMBRES_MIN: 3,
    NOMBRES_MAX: 25,
    APELLIDOS_MIN: 3,
    APELLIDOS_MAX: 25,
    DNI: 8,
    TELEFONO: 9,
    CORREO_MAX: 40,
    AREA_MIN: 3,
    AREA_MAX: 50,
  };

  // Forzar solo letras en Nombres y Apellidos
  nombres.addEventListener("input", (e) => {
    e.target.value = e.target.value
      .replace(/[^a-záéíóúñA-ZÁÉÍÓÚÑ\s]/g, "")
      .slice(0, LIMITES.NOMBRES_MAX);
  });

  apellidos.addEventListener("input", (e) => {
    e.target.value = e.target.value
      .replace(/[^a-záéíóúñA-ZÁÉÍÓÚÑ\s]/g, "")
      .slice(0, LIMITES.APELLIDOS_MAX);
  });

  // Forzar solo números en DNI y Teléfono
  dni.addEventListener("input", (e) => {
    e.target.value = e.target.value.replace(/[^\d]/g, "").slice(0, LIMITES.DNI);
  });

  telefono.addEventListener("input", (e) => {
    e.target.value = e.target.value
      .replace(/[^\d]/g, "")
      .slice(0, LIMITES.TELEFONO);
  });

  // Área ahora es un combo; no aplicamos filtrado de texto

  // Limitar correo electrónico
  correoElectronico.addEventListener("input", (e) => {
    if (e.target.value.length > LIMITES.CORREO_MAX) {
      e.target.value = e.target.value.slice(0, LIMITES.CORREO_MAX);
    }
  });

  form.addEventListener("submit", (e) => {
    console.log("Submit detectado");
    e.preventDefault();
    validaCampos();
  });

  const validaCampos = () => {
    console.log("Validando campos...");

    // Capturar los valores ingresados por el usuario
    const tipoUsuarioValor = rolSelect.value.trim();

    const nombresValor = nombres.value.trim();
    const apellidosValor = apellidos.value.trim();
    const dniValor = dni.value.trim();
    const telefonoValor = telefono.value.trim();
    const correoElectronicoValor = correoElectronico.value.trim();
    const areaValor = area.value.trim();

    console.log("Valores capturados:", {
      tipoUsuario: tipoUsuarioValor,
      nombres: nombresValor,
      apellidos: apellidosValor,
      dni: dniValor,
      telefono: telefonoValor,
      correo: correoElectronicoValor,
      area: areaValor,
    });

    let esValido = true;

    // Validar Tipo de Usuario (combobox)
    if (!tipoUsuarioValor) {
      validaFalla(rolSelect, "Debe seleccionar un tipo de usuario");
      esValido = false;
    } else {
      validaOK(rolSelect);
    }

    // Validar Nombres
    if (nombresValor === "") {
      validaFalla(nombres, "El campo nombres es obligatorio");
      esValido = false;
    } else if (nombresValor.length < LIMITES.NOMBRES_MIN) {
      validaFalla(
        nombres,
        `El nombre debe tener al menos ${LIMITES.NOMBRES_MIN} caracteres`
      );
      esValido = false;
    } else if (nombresValor.length > LIMITES.NOMBRES_MAX) {
      validaFalla(
        nombres,
        `El nombre no puede exceder ${LIMITES.NOMBRES_MAX} caracteres`
      );
      esValido = false;
    } else if (!/^[a-záéíóúñA-ZÁÉÍÓÚÑ\s]+$/.test(nombresValor)) {
      validaFalla(nombres, "El nombre solo debe contener letras");
      esValido = false;
    } else {
      validaOK(nombres);
    }

    // Validar Apellidos
    if (apellidosValor === "") {
      validaFalla(apellidos, "El campo apellidos es obligatorio");
      esValido = false;
    } else if (apellidosValor.length < LIMITES.APELLIDOS_MIN) {
      validaFalla(
        apellidos,
        `El apellido debe tener al menos ${LIMITES.APELLIDOS_MIN} caracteres`
      );
      esValido = false;
    } else if (apellidosValor.length > LIMITES.APELLIDOS_MAX) {
      validaFalla(
        apellidos,
        `El apellido no puede exceder ${LIMITES.APELLIDOS_MAX} caracteres`
      );
      esValido = false;
    } else if (!/^[a-záéíóúñA-ZÁÉÍÓÚÑ\s]+$/.test(apellidosValor)) {
      validaFalla(apellidos, "El apellido solo debe contener letras");
      esValido = false;
    } else {
      validaOK(apellidos);
    }

    // Validar DNI
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

    // Validar Teléfono
    if (telefonoValor === "") {
      validaFalla(telefono, "El campo teléfono es obligatorio");
      esValido = false;
    } else if (telefonoValor.length !== LIMITES.TELEFONO) {
      validaFalla(
        telefono,
        `El teléfono debe tener exactamente ${LIMITES.TELEFONO} dígitos`
      );
      esValido = false;
    } else if (!/^9\d{8}$/.test(telefonoValor)) {
      validaFalla(telefono, "El teléfono debe iniciar con 9 y tener 9 dígitos");
      esValido = false;
    } else {
      validaOK(telefono);
    }

    // Validar Correo Electrónico
    if (correoElectronicoValor === "") {
      validaFalla(
        correoElectronico,
        "El campo correo electrónico es obligatorio"
      );
      esValido = false;
    } else if (correoElectronicoValor.length > LIMITES.CORREO_MAX) {
      validaFalla(
        correoElectronico,
        `El correo no puede exceder ${LIMITES.CORREO_MAX} caracteres`
      );
      esValido = false;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correoElectronicoValor)) {
      validaFalla(correoElectronico, "Ingrese un correo electrónico válido");
      esValido = false;
    } else {
      validaOK(correoElectronico);
    }

    // Validar Área (combobox GUID)
    if (!areaValor) {
      validaFalla(area, "Debe seleccionar un área");
      esValido = false;
    } else {
      validaOK(area);
    }

    console.log("¿Es válido?:", esValido);

    // Si todo es válido, enviar el formulario
    if (esValido) {
      console.log("Enviando formulario...");
      form.submit();
    } else {
      console.log("Formulario NO válido, no se envía");
    }
  };

  const validaFalla = (input, mensaje) => {
    const inputBox = input.parentElement;
    const avisoAnterior = inputBox.querySelector(".aviso");
    if (avisoAnterior) {
      avisoAnterior.remove();
    }
    const aviso = document.createElement("small");
    aviso.className = "aviso";
    aviso.style.color = "#e74c3c";
    aviso.style.fontSize = "13px";
    aviso.style.display = "block";
    aviso.style.marginTop = "5px";
    aviso.innerText = mensaje;
    inputBox.appendChild(aviso);
    input.style.borderColor = "#e74c3c";
  };

  const validaOK = (input) => {
    const inputBox = input.parentElement;
    const aviso = inputBox.querySelector(".aviso");
    if (aviso) {
      aviso.remove();
    }
    input.style.borderColor = "#28a745";
  };

  const validaFallaRadio = (contenedor, mensaje) => {
    const avisoAnterior = contenedor.querySelector(".aviso");
    if (avisoAnterior) {
      avisoAnterior.remove();
    }
    const aviso = document.createElement("small");
    aviso.className = "aviso";
    aviso.style.color = "#e74c3c";
    aviso.style.fontSize = "13px";
    aviso.style.display = "block";
    aviso.style.marginTop = "5px";
    aviso.style.clear = "both";
    aviso.style.width = "100%";
    aviso.innerText = mensaje;
    const category = contenedor.querySelector(".category");
    if (category) {
      category.insertAdjacentElement("afterend", aviso);
    } else {
      contenedor.appendChild(aviso);
    }
  };

  const validaOKRadio = (contenedor) => {
    const aviso = contenedor.querySelector(".aviso");
    if (aviso) {
      aviso.remove();
    }
  };

  // Validación en tiempo real (resto del código igual)
  rolSelect.addEventListener("change", () => {
    if (rolSelect.value.trim()) {
      validaOK(rolSelect);
    }
  });

  nombres.addEventListener("blur", () => {
    const valor = nombres.value.trim();
    if (
      valor &&
      valor.length >= LIMITES.NOMBRES_MIN &&
      valor.length <= LIMITES.NOMBRES_MAX &&
      /^[a-záéíóúñA-ZÁÉÍÓÚÑ\s]+$/.test(valor)
    ) {
      validaOK(nombres);
    }
  });

  apellidos.addEventListener("blur", () => {
    const valor = apellidos.value.trim();
    if (
      valor &&
      valor.length >= LIMITES.APELLIDOS_MIN &&
      valor.length <= LIMITES.APELLIDOS_MAX &&
      /^[a-záéíóúñA-ZÁÉÍÓÚÑ\s]+$/.test(valor)
    ) {
      validaOK(apellidos);
    }
  });

  dni.addEventListener("blur", () => {
    const valor = dni.value.trim();
    if (valor.length === LIMITES.DNI && /^\d{8}$/.test(valor)) {
      validaOK(dni);
    }
  });

  telefono.addEventListener("blur", () => {
    const valor = telefono.value.trim();
    if (valor.length === LIMITES.TELEFONO && /^9\d{8}$/.test(valor)) {
      validaOK(telefono);
    }
  });

  correoElectronico.addEventListener("blur", () => {
    const valor = correoElectronico.value.trim();
    if (valor && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(valor)) {
      validaOK(correoElectronico);
    }
  });

  area.addEventListener("blur", () => {
    if (area.value.trim()) {
      validaOK(area);
    }
  });
});
