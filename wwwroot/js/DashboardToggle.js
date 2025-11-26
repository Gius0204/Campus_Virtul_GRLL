/**
 * ========================================
 * JAVASCRIPT PARA DASHBOARD RESPONSIVE
 * Archivo: DashboardToggle.js
 * ========================================
 */

(function () {
    'use strict';

    // Esperar a que el DOM esté completamente cargado
    document.addEventListener('DOMContentLoaded', function () {
        console.log('🎯 Dashboard JavaScript inicializado');

        // ========================================
        // ELEMENTOS DEL DOM
        // ========================================
        const menuToggleBtn = document.getElementById('menuToggleBtn');
        const sidebar = document.querySelector('.sidebar');
        const mainContent = document.querySelector('.main-content');
        const overlay = document.querySelector('.sidebar-overlay');

        // Verificar que los elementos existen
        if (!menuToggleBtn || !sidebar || !mainContent) {
            console.error('❌ No se encontraron elementos necesarios del dashboard');
            return;
        }

        console.log('✅ Elementos del dashboard encontrados');

        // ========================================
        // DETECTAR TAMAÑO DE PANTALLA
        // ========================================
        const isMobile = () => window.innerWidth <= 768;
        const isTablet = () => window.innerWidth > 768 && window.innerWidth <= 1024;
        const isDesktop = () => window.innerWidth > 1024;

        // ========================================
        // INICIALIZAR ESTADO SEGÚN PANTALLA
        // ========================================
        function initializeDashboard() {
            if (isMobile()) {
                // Móvil: sidebar oculto, contenido expandido
                sidebar.classList.remove('active');
                sidebar.classList.add('collapsed');
                mainContent.classList.add('expanded');
                if (overlay) overlay.classList.remove('active');
                console.log('📱 Modo móvil activado');
            } else {
                // Desktop/Tablet: sidebar visible, contenido normal
                sidebar.classList.remove('collapsed', 'active');
                mainContent.classList.remove('expanded');
                if (overlay) overlay.classList.remove('active');
                console.log('💻 Modo escritorio activado');
            }
        }

        // Inicializar al cargar
        initializeDashboard();

        // ========================================
        // TOGGLE DEL MENÚ
        // ========================================
        function toggleSidebar() {
            if (isMobile()) {
                // En móvil: mostrar/ocultar con overlay
                const isActive = sidebar.classList.contains('active');

                if (isActive) {
                    sidebar.classList.remove('active');
                    if (overlay) overlay.classList.remove('active');
                    console.log('📱 Menú cerrado (móvil)');
                } else {
                    sidebar.classList.add('active');
                    if (overlay) overlay.classList.add('active');
                    console.log('📱 Menú abierto (móvil)');
                }
            } else {
                // En desktop: colapsar/expandir
                const isCollapsed = sidebar.classList.contains('collapsed');

                if (isCollapsed) {
                    sidebar.classList.remove('collapsed');
                    mainContent.classList.remove('expanded');
                    console.log('💻 Menú expandido (escritorio)');
                } else {
                    sidebar.classList.add('collapsed');
                    mainContent.classList.add('expanded');
                    console.log('💻 Menú colapsado (escritorio)');
                }
            }
        }

        // Event listener para el botón toggle
        menuToggleBtn.addEventListener('click', function (e) {
            e.preventDefault();
            toggleSidebar();
        });

        // ========================================
        // CERRAR MENÚ AL HACER CLIC EN OVERLAY
        // ========================================
        if (overlay) {
            overlay.addEventListener('click', function () {
                if (isMobile()) {
                    sidebar.classList.remove('active');
                    overlay.classList.remove('active');
                    console.log('📱 Menú cerrado por overlay');
                }
            });
        }

        // ========================================
        // CERRAR MENÚ AL HACER CLIC EN UN LINK (SOLO MÓVIL)
        // ========================================
        const menuItems = document.querySelectorAll('.menu-item');
        menuItems.forEach(function (item) {
            item.addEventListener('click', function () {
                if (isMobile()) {
                    sidebar.classList.remove('active');
                    if (overlay) overlay.classList.remove('active');
                    console.log('📱 Menú cerrado por clic en item');
                }
            });
        });

        // ========================================
        // MANEJAR CAMBIOS DE TAMAÑO DE VENTANA
        // ========================================
        let resizeTimer;
        let previousWidth = window.innerWidth;

        window.addEventListener('resize', function () {
            // Debounce para evitar múltiples llamadas
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () {
                const currentWidth = window.innerWidth;

                // Detectar cambios entre móvil ↔ desktop
                if ((previousWidth <= 768 && currentWidth > 768) ||
                    (previousWidth > 768 && currentWidth <= 768)) {
                    console.log('🔄 Cambio de modo detectado');
                    initializeDashboard();
                }

                previousWidth = currentWidth;
            }, 250);
        });

        // ========================================
        // CERRAR MENÚ CON TECLA ESC (SOLO MÓVIL)
        // ========================================
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && isMobile()) {
                if (sidebar.classList.contains('active')) {
                    sidebar.classList.remove('active');
                    if (overlay) overlay.classList.remove('active');
                    console.log('📱 Menú cerrado con ESC');
                }
            }
        });

        // ========================================
        // PREVENIR SCROLL DEL BODY CUANDO MENÚ ESTÁ ABIERTO (MÓVIL)
        // ========================================
        const observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                if (mutation.attributeName === 'class') {
                    if (isMobile() && sidebar.classList.contains('active')) {
                        document.body.style.overflow = 'hidden';
                    } else {
                        document.body.style.overflow = '';
                    }
                }
            });
        });

        observer.observe(sidebar, {
            attributes: true,
            attributeFilter: ['class']
        });

        // ========================================
        // MANEJAR ITEMS ACTIVOS DEL MENÚ
        // ========================================
        function setActiveMenuItem() {
            const currentPath = window.location.pathname;

            menuItems.forEach(function (item) {
                const itemHref = item.getAttribute('href');

                if (itemHref && currentPath.includes(itemHref)) {
                    item.classList.add('active');
                } else {
                    item.classList.remove('active');
                }
            });
        }

        // Establecer item activo al cargar
        setActiveMenuItem();

        // ========================================
        // GUARDAR ESTADO DEL MENÚ EN LOCALSTORAGE (OPCIONAL)
        // ========================================
        function saveMenuState() {
            if (!isMobile()) {
                const isCollapsed = sidebar.classList.contains('collapsed');
                localStorage.setItem('dashboardMenuCollapsed', isCollapsed);
            }
        }

        function loadMenuState() {
            if (!isMobile()) {
                const wasCollapsed = localStorage.getItem('dashboardMenuCollapsed') === 'true';
                if (wasCollapsed) {
                    sidebar.classList.add('collapsed');
                    mainContent.classList.add('expanded');
                }
            }
        }

        // Cargar estado guardado
        loadMenuState();

        // Guardar estado al cambiar
        menuToggleBtn.addEventListener('click', function () {
            setTimeout(saveMenuState, 300);
        });

        // ========================================
        // ANIMACIÓN DE ENTRADA
        // ========================================
        setTimeout(function () {
            sidebar.style.opacity = '1';
            mainContent.style.opacity = '1';
        }, 100);

        console.log('✅ Dashboard completamente inicializado');
    });

})();