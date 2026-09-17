package com.fromthefarm.app.data

import retrofit2.http.*

// Preserve this REST contract when the backend is deployed.
data class Location(val latitude: Double, val longitude: Double)
data class Profile(val userId: String, val displayName: String, val role: String?, val language: String,
    val searchRadiusKm: Int, val notificationsEnabled: Boolean, val biometricLockEnabled: Boolean, val phone: String?)
data class ProfileUpdate(val role: String, val language: String, val searchRadiusKm: Int,
    val notificationsEnabled: Boolean, val biometricLockEnabled: Boolean, val phone: String?)
data class SessionRequest(val deviceLanguage: String)
data class Session(val userId: String, val onboardingComplete: Boolean)
data class Listing(val id: String, val cropType: String, val quantity: Double, val unit: String,
    val harvestDate: String, val location: Location, val status: String, val photoUrl: String? = null)
data class Demand(val id: String, val cropType: String, val quantityNeeded: Double, val unit: String,
    val deadline: String, val location: Location, val status: String)
data class ListingWrite(val cropType: String, val quantity: Double, val unit: String,
    val harvestDate: String, val location: Location, val photoBase64: String? = null)
data class DemandWrite(val cropType: String, val quantityNeeded: Double, val unit: String,
    val deadline: String, val location: Location)
data class Counterpart(val cropType: String, val quantity: Double, val unit: String,
    val distanceKm: Double, val relevantDate: String)
data class Match(val matchId: String, val score: Double, val counterpart: Counterpart, val status: String)
data class Contact(val displayName: String?, val phone: String?)
data class MatchDetail(val matchId: String, val score: Double, val status: String, val counterpartContact: Contact? = null)
data class RatingWrite(val thumbsUp: Boolean)

interface FarmApi {
    @POST("auth/session") suspend fun session(@Header("Authorization") token: String, @Body body: SessionRequest): Session
    @GET("users/me") suspend fun profile(@Header("Authorization") token: String): Profile
    @PUT("users/me") suspend fun profile(@Header("Authorization") token: String, @Body body: ProfileUpdate): Profile
    @GET("listings") suspend fun listings(@Header("Authorization") token: String, @Query("mine") mine: Boolean): List<Listing>
    @GET("listings") suspend fun browseListings(@Header("Authorization") token: String,
        @Query("cropType") crop: String?, @Query("maxDistanceKm") radius: Int,
        @Query("latitude") latitude: Double, @Query("longitude") longitude: Double): List<Listing>
    @POST("listings") suspend fun createListing(@Header("Authorization") token: String, @Body body: ListingWrite): Listing
    @PUT("listings/{id}") suspend fun updateListing(@Header("Authorization") token: String, @Path("id") id: String, @Body body: ListingWrite): Listing
    @DELETE("listings/{id}") suspend fun deleteListing(@Header("Authorization") token: String, @Path("id") id: String)
    @GET("demands") suspend fun demands(@Header("Authorization") token: String, @Query("mine") mine: Boolean = true): List<Demand>
    @POST("demands") suspend fun createDemand(@Header("Authorization") token: String, @Body body: DemandWrite): Demand
    @PUT("demands/{id}") suspend fun updateDemand(@Header("Authorization") token: String, @Path("id") id: String, @Body body: DemandWrite): Demand
    @DELETE("demands/{id}") suspend fun deleteDemand(@Header("Authorization") token: String, @Path("id") id: String)
    @GET("matches") suspend fun matches(@Header("Authorization") token: String): List<Match>
    @GET("matches/{id}") suspend fun detail(@Header("Authorization") token: String, @Path("id") id: String): MatchDetail
    @POST("matches/{id}/confirm") suspend fun confirm(@Header("Authorization") token: String, @Path("id") id: String)
    @POST("matches/{id}/complete") suspend fun complete(@Header("Authorization") token: String, @Path("id") id: String)
    @POST("matches/{id}/rating") suspend fun rate(@Header("Authorization") token: String, @Path("id") id: String, @Body body: RatingWrite)
}
