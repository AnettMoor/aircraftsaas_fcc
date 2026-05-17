<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">My Reviews</h1>
      </div>
    </div>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadReviews" />

    <EmptyState
      v-else-if="myReviews.length === 0"
      icon="⭐"
      title="No reviews yet"
      description="After completing a booking, you can leave a review for the aircraft."
    >
      <template #action>
        <AppButton @click="$router.push({ name: 'booking-list' })">View my bookings</AppButton>
      </template>
    </EmptyState>

    <div v-else class="flex flex-col gap-4">
      <div
        v-for="review in myReviews"
        :key="review.id"
        class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm transition-shadow hover:shadow-md"
      >
        <div class="flex justify-between items-start mb-3">
          <div class="flex flex-col gap-0.5">
            <span class="text-lg font-semibold text-slate-900">{{ review.aircraftName }}</span>
            <span class="text-sm text-slate-500">{{ formatDate(review.reviewedAt) }}</span>
          </div>
          <div class="flex gap-2">
            <button
              class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors"
              @click="editReview(review)"
              title="Edit review"
            >✏</button>
            <button
              class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors"
              @click="confirmDelete(review)"
              title="Delete review"
            >🗑</button>
          </div>
        </div>

        <div class="flex items-center gap-1 mb-2">
          <span
            v-for="star in 5"
            :key="star"
            :class="['text-lg', star <= review.rating ? 'text-amber-400' : 'text-slate-200']"
          >★</span>
          <span class="text-sm text-slate-500 ml-1">{{ review.rating }}/5</span>
        </div>

        <p v-if="review.comment" class="text-base text-slate-600 m-0 mb-2 leading-relaxed">{{ review.comment }}</p>
        <span v-if="review.reviewType" class="inline-block bg-blue-50 text-blue-700 text-xs font-medium px-2 py-0.5 rounded-full mr-2">{{ review.reviewType }}</span>
        <span v-if="review.isVerifiedBooking" class="text-xs text-emerald-500 font-medium">✓ Verified booking</span>
      </div>
    </div>

    <!-- Edit Modal -->
    <div v-if="editingReview" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50" @click.self="cancelEdit">
      <div class="bg-white rounded-xl shadow-lg w-full max-w-lg mx-4 animate-modal-enter">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h2 class="text-lg font-semibold text-slate-900">Edit Review</h2>
          <button class="text-slate-400 hover:text-slate-600 text-2xl leading-none transition-colors" @click="cancelEdit">×</button>
        </div>
        <div class="px-6 pt-4">
          <p class="text-base text-slate-500 mb-5">{{ editingReview.aircraftName }}</p>

          <AppAlert v-if="editError" type="error" class="mb-4">{{ editError }}</AppAlert>

          <form @submit.prevent="saveEdit" class="flex flex-col gap-4">
            <div class="flex flex-col gap-1">
              <label class="text-base font-medium text-slate-600">Rating</label>
              <div class="flex gap-1">
                <button
                  v-for="star in 5"
                  :key="star"
                  type="button"
                  :class="[
                    'bg-transparent border-none text-3xl cursor-pointer p-0 transition-all',
                    star <= editForm.rating ? 'text-amber-400' : 'text-slate-200',
                    'hover:text-amber-400 hover:scale-110'
                  ]"
                  @click="editForm.rating = star"
                >★</button>
              </div>
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-base font-medium text-slate-600" for="edit-comment">Comment</label>
              <textarea
                id="edit-comment"
                v-model="editForm.comment"
                class="w-full px-3 py-2 border border-slate-200 rounded-lg text-base text-slate-900 resize-y font-sans transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10"
                rows="4"
                placeholder="Share your experience..."
              ></textarea>
            </div>

            <div class="flex flex-col gap-1">
              <label class="text-base font-medium text-slate-600" for="edit-type">Review type</label>
              <select
                id="edit-type"
                v-model="editForm.reviewType"
                class="w-full px-3 py-2 border border-slate-200 rounded-lg text-base text-slate-900 bg-white transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10"
              >
                <option value="">General</option>
                <option value="Safety">Safety</option>
                <option value="Comfort">Comfort</option>
                <option value="Performance">Performance</option>
                <option value="ValueForMoney">Value for Money</option>
              </select>
            </div>

            <div class="flex justify-end gap-3 pt-2 pb-4">
              <AppButton type="button" variant="secondary" @click="cancelEdit">Cancel</AppButton>
              <AppButton type="submit" :loading="saving">Save changes</AppButton>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Delete Confirm Modal -->
    <div v-if="deletingReview" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50" @click.self="cancelDelete">
      <div class="bg-white rounded-xl shadow-lg w-full max-w-sm mx-4 animate-modal-enter">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h2 class="text-lg font-semibold text-slate-900">Delete Review</h2>
          <button class="text-slate-400 hover:text-slate-600 text-2xl leading-none transition-colors" @click="cancelDelete">×</button>
        </div>
        <div class="px-6 py-4">
          <p class="text-base text-slate-600">Are you sure you want to delete your review for <strong>{{ deletingReview.aircraftName }}</strong>? This action cannot be undone.</p>
        </div>
        <div class="px-6 pb-4">
          <AppAlert v-if="deleteError" type="error" class="mb-4">{{ deleteError }}</AppAlert>
          <div class="flex justify-end gap-3">
            <AppButton type="button" variant="secondary" @click="cancelDelete">Cancel</AppButton>
            <AppButton variant="danger" :loading="deleting" @click="doDelete">Delete</AppButton>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { reviewService } from '@/api'
import { useSessionStore } from '@/stores/sessionStore'
import type { ReviewDto } from '@/types/review'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const sessionStore = useSessionStore()

const allReviews = ref<ReviewDto[]>([])
const myReviews = ref<ReviewDto[]>([])
const loading = ref(false)
const error = ref('')

// Edit state
const editingReview = ref<ReviewDto | null>(null)
const editForm = ref({ rating: 5, comment: '', reviewType: '' })
const saving = ref(false)
const editError = ref('')

// Delete state
const deletingReview = ref<ReviewDto | null>(null)
const deleting = ref(false)
const deleteError = ref('')

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

async function loadReviews() {
  loading.value = true
  error.value = ''
  try {
    allReviews.value = await reviewService.getAll()
    // Filter to only show the current user's reviews
    const userId = sessionStore.user?.id
    if (userId) {
      myReviews.value = allReviews.value.filter(r => r.authorId === userId)
    } else {
      myReviews.value = allReviews.value
    }
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load reviews'
  } finally {
    loading.value = false
  }
}

function editReview(review: ReviewDto) {
  editingReview.value = review
  editForm.value = {
    rating: review.rating,
    comment: review.comment ?? '',
    reviewType: review.reviewType ?? '',
  }
  editError.value = ''
}

function cancelEdit() {
  editingReview.value = null
  editError.value = ''
}

async function saveEdit() {
  if (!editingReview.value) return
  saving.value = true
  editError.value = ''
  try {
    const updated = await reviewService.update(editingReview.value.id, {
      rating: editForm.value.rating,
      comment: editForm.value.comment || undefined,
      reviewType: editForm.value.reviewType || undefined,
    })
    // Update in list
    const idx = myReviews.value.findIndex(r => r.id === updated.id)
    if (idx !== -1) myReviews.value[idx] = updated
    editingReview.value = null
  } catch (err: unknown) {
    editError.value = err instanceof Error ? err.message : 'Failed to update review'
  } finally {
    saving.value = false
  }
}

function confirmDelete(review: ReviewDto) {
  deletingReview.value = review
  deleteError.value = ''
}

function cancelDelete() {
  deletingReview.value = null
  deleteError.value = ''
}

async function doDelete() {
  if (!deletingReview.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await reviewService.delete(deletingReview.value.id)
    myReviews.value = myReviews.value.filter(r => r.id !== deletingReview.value!.id)
    deletingReview.value = null
  } catch (err: unknown) {
    deleteError.value = err instanceof Error ? err.message : 'Failed to delete review'
  } finally {
    deleting.value = false
  }
}

onMounted(loadReviews)
</script>
