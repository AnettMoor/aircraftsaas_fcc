// ============================================================
// Route Definitions
// meta.requiresAuth   = true → must be logged in
// meta.requiresRole   = 'CompanyOwner' → must be company owner
// meta.guestOnly      = true → redirect if already logged in
// ============================================================

import type { RouteRecordRaw } from 'vue-router'

export const routes: RouteRecordRaw[] = [
  // ----------------------------------------------------------------
  // Root redirect
  // ----------------------------------------------------------------
  {
    path: '/',
    redirect: '/client/dashboard',
  },

  // ----------------------------------------------------------------
  // Auth routes (public — redirect to dashboard if logged in)
  // ----------------------------------------------------------------
  {
    path: '/auth',
    component: () => import('@/layouts/AuthLayout.vue'),
    children: [
      {
        path: 'login',
        name: 'login',
        component: () => import('@/views/auth/LoginView.vue'),
        meta: { guestOnly: true, title: 'Login' },
      },
      {
        path: 'register',
        name: 'register',
        component: () => import('@/views/auth/RegisterView.vue'),
        meta: { guestOnly: true, title: 'Register' },
      },
      {
        path: 'forgot-password',
        name: 'forgot-password',
        component: () => import('@/views/auth/ForgotPasswordView.vue'),
        meta: { guestOnly: true, title: 'Forgot Password' },
      },
    ],
  },

  // ----------------------------------------------------------------
  // Client area — any authenticated user (pilot or company owner)
  // ----------------------------------------------------------------
  {
    path: '/client',
    component: () => import('@/layouts/ClientLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        redirect: { name: 'client-dashboard' },
      },
      {
        path: 'dashboard',
        name: 'client-dashboard',
        component: () => import('@/views/client/ClientDashboardView.vue'),
        meta: { requiresAuth: true, title: 'Dashboard' },
      },
      {
        path: 'aircraft',
        name: 'aircraft-list',
        component: () => import('@/views/client/AircraftListView.vue'),
        meta: { requiresAuth: true, title: 'Browse Aircraft' },
      },
      {
        path: 'aircraft/:id',
        name: 'aircraft-detail',
        component: () => import('@/views/client/AircraftDetailView.vue'),
        meta: { requiresAuth: true, title: 'Aircraft Details' },
        props: true,
      },
      {
        path: 'bookings',
        name: 'booking-list',
        component: () => import('@/views/client/BookingListView.vue'),
        meta: { requiresAuth: true, title: 'My Bookings' },
      },
      {
        path: 'bookings/:id',
        name: 'booking-detail',
        component: () => import('@/views/client/BookingDetailView.vue'),
        meta: { requiresAuth: true, title: 'Booking Details' },
        props: true,
      },
      {
        path: 'bookings/:id/edit',
        name: 'booking-edit',
        component: () => import('@/views/client/BookingEditView.vue'),
        meta: { requiresAuth: true, title: 'Edit Booking' },
        props: true,
      },
      {
        path: 'reviews',
        name: 'review-list',
        component: () => import('@/views/client/ReviewListView.vue'),
        meta: { requiresAuth: true, title: 'My Reviews' },
      },
      {
        path: 'reviews/new/:bookingId',
        name: 'review-create',
        component: () => import('@/views/client/ReviewFormView.vue'),
        meta: { requiresAuth: true, title: 'Write a Review' },
        props: true,
      },
      {
        path: 'licenses',
        name: 'license-list',
        component: () => import('@/views/client/LicenseListView.vue'),
        meta: { requiresAuth: true, title: 'My Licenses' },
      },
      {
        path: 'profile',
        name: 'client-profile',
        component: () => import('@/views/client/ProfileView.vue'),
        meta: { requiresAuth: true, title: 'My Profile' },
      },
    ],
  },

  // ----------------------------------------------------------------
  // Admin area — CompanyOwner role required
  // ----------------------------------------------------------------
  {
    path: '/admin',
    component: () => import('@/layouts/AdminLayout.vue'),
    meta: { requiresAuth: true, requiresRole: 'CompanyOwner' },
    children: [
      {
        path: '',
        redirect: { name: 'admin-dashboard' },
      },
      {
        path: 'dashboard',
        name: 'admin-dashboard',
        component: () => import('@/views/admin/AdminDashboardView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Admin Dashboard' },
      },
      {
        path: 'aircraft',
        name: 'admin-aircraft',
        component: () => import('@/views/admin/AdminAircraftView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Manage Aircraft' },
      },
      {
        path: 'aircraft/:id',
        name: 'admin-aircraft-detail',
        component: () => import('@/views/admin/AdminAircraftDetailView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Aircraft Detail' },
        props: true,
      },
      {
        path: 'bookings',
        name: 'admin-bookings',
        component: () => import('@/views/admin/AdminBookingsView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Manage Bookings' },
      },
      {
        path: 'bookings/:id',
        name: 'admin-booking-detail',
        component: () => import('@/views/admin/AdminBookingDetailView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Booking Detail' },
        props: true,
      },
      {
        path: 'maintenance',
        name: 'admin-maintenance',
        component: () => import('@/views/admin/AdminMaintenanceView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Maintenance Records' },
      },
      {
        path: 'settings',
        name: 'admin-settings',
        component: () => import('@/views/admin/AdminCompanySettingsView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'Company Settings' },
      },
      {
        path: 'profile',
        name: 'admin-profile',
        component: () => import('@/views/client/ProfileView.vue'),
        meta: { requiresAuth: true, requiresRole: 'CompanyOwner', title: 'My Profile' },
      },
    ],
  },

  // ----------------------------------------------------------------
  // 404 catch-all
  // ----------------------------------------------------------------
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { title: 'Page Not Found' },
  },
]
